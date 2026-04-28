"""Hybrid RAG retrieval (pgvector + strict metadata filters) and prompt assembly."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any

import numpy as np
from psycopg2.extensions import connection as PGConnection

from app.core.db import modality_for_db
from app.services.embeddings.text_encoder import encode_text


@dataclass(frozen=True)
class RetrievalHit:
    source: str
    ref_id: str
    content: str
    pathology_group: str | None
    distance: float


_PATHOLOGY_PROMPT_HINTS: dict[str, str] = {
    "Trauma": (
        "Focus on acute injury patterns, fracture lines, displacement, soft-tissue swelling, "
        "and complications. Prioritize stability assessment and urgent red flags."
    ),
    "Tumor": (
        "Focus on bone lesion characterization (margins, matrix, periosteal reaction), "
        "aggressive features, and differential between benign vs malignant patterns."
    ),
    "Degenerative": (
        "Focus on chronic joint space loss, osteophytes, subchondral changes, and alignment; "
        "differentiate degenerative findings from acute injury."
    ),
    "Inflammation": (
        "Focus on inflammatory signs, infection mimics, septic arthritis features, "
        "and systemic correlation where relevant."
    ),
    "Congenital": (
        "Focus on developmental morphology, growth plates, dysplasia patterns, "
        "and age-appropriate norms."
    ),
}


def _pathology_instruction(pathology_group: str | None) -> str:
    if not pathology_group:
        return "Use general musculoskeletal radiology reasoning."
    return _PATHOLOGY_PROMPT_HINTS.get(
        pathology_group,
        "Use general musculoskeletal radiology reasoning.",
    )


def hybrid_retrieve(
    conn: PGConnection,
    *,
    user_question: str,
    query_text_vector: np.ndarray,
    image_vector: np.ndarray | None,
    modality: str,
    anatomy: str,
    pathology_group: str | None = None,
    per_source_limit: int = 3,
    final_top_k: int = 5,
) -> list[RetrievalHit]:
    """
    HNSW-backed cosine distance via `<=>` operator; metadata filters are equality predicates
    on indexed columns (Hybrid Search).
    """
    mod = modality_for_db(modality)
    ana = anatomy
    pg = pathology_group
    qvec = query_text_vector.astype(np.float32)

    hits: list[RetrievalHit] = []

    with conn.cursor() as cur:
        cur.execute(
            """
            SELECT 'case_text'::text,
                   cte.case_id::text,
                   COALESCE(cte.source_text, ''),
                   cm.pathology_group,
                   (cte.text_vector <=> %(q)s::vector)::float8 AS dist
            FROM public.case_text_embeddings AS cte
            INNER JOIN public.case_metadata AS cm ON cm.case_id = cte.case_id
            WHERE cte.text_vector IS NOT NULL
              AND cm.modality = %(mod)s
              AND cm.anatomy = %(ana)s
              AND (%(pg)s::text IS NULL OR cm.pathology_group = %(pg)s)
            ORDER BY cte.text_vector <=> %(q)s::vector
            LIMIT %(lim)s;
            """,
            {"q": qvec, "mod": mod, "ana": ana, "pg": pg, "lim": per_source_limit},
        )
        for row in cur.fetchall():
            hits.append(
                RetrievalHit(
                    source=row[0],
                    ref_id=row[1],
                    content=row[2][:8000],
                    pathology_group=row[3],
                    distance=float(row[4]),
                )
            )

        cur.execute(
            """
            SELECT 'doc_chunk'::text,
                   dc.id::text,
                   dc.content,
                   dc.pathology_group,
                   (dc.embedding <=> %(q)s::vector)::float8 AS dist
            FROM public.document_chunks AS dc
            WHERE dc.embedding IS NOT NULL
              AND dc.modality = %(mod)s
              AND dc.anatomy = %(ana)s
              AND (%(pg)s::text IS NULL OR dc.pathology_group = %(pg)s)
            ORDER BY dc.embedding <=> %(q)s::vector
            LIMIT %(lim)s;
            """,
            {"q": qvec, "mod": mod, "ana": ana, "pg": pg, "lim": per_source_limit},
        )
        for row in cur.fetchall():
            hits.append(
                RetrievalHit(
                    source=row[0],
                    ref_id=row[1],
                    content=row[2][:8000],
                    pathology_group=row[3],
                    distance=float(row[4]),
                )
            )

        if image_vector is not None:
            ivec = image_vector.astype(np.float32)
            cur.execute(
                """
                SELECT 'case_image'::text,
                       cme.id::text,
                       COALESCE(m.media_url, ''),
                       cm.pathology_group,
                       (cme.image_vector <=> %(iv)s::vector)::float8 AS dist
                FROM public.case_media_embeddings AS cme
                INNER JOIN public.case_media AS m ON m.id = cme.media_id
                INNER JOIN public.case_metadata AS cm ON cm.case_id = m.case_id
                WHERE cme.image_vector IS NOT NULL
                  AND cm.modality = %(mod)s
                  AND cm.anatomy = %(ana)s
                  AND (%(pg)s::text IS NULL OR cm.pathology_group = %(pg)s)
                ORDER BY cme.image_vector <=> %(iv)s::vector
                LIMIT %(lim)s;
                """,
                {"iv": ivec, "mod": mod, "ana": ana, "pg": pg, "lim": per_source_limit},
            )
            for row in cur.fetchall():
                hits.append(
                    RetrievalHit(
                        source=row[0],
                        ref_id=row[1],
                        content=row[2][:8000],
                        pathology_group=row[3],
                        distance=float(row[4]),
                    )
                )

    hits.sort(key=lambda h: h.distance)
    deduped: list[RetrievalHit] = []
    seen: set[tuple[str, str]] = set()
    for h in hits:
        key = (h.source, h.ref_id)
        if key in seen:
            continue
        seen.add(key)
        deduped.append(h)
        if len(deduped) >= final_top_k:
            break

    return deduped[:final_top_k]


def build_llm_prompt(
    *,
    user_question: str,
    hits: list[RetrievalHit],
    dominant_pathology: str | None,
) -> tuple[str, list[dict[str, Any]]]:
    """Returns (system_or_combined_prompt, context_blocks_for_client)."""
    pathology = dominant_pathology or (
        next((h.pathology_group for h in hits if h.pathology_group), None)
    )
    hint = _pathology_instruction(pathology)

    context_blocks: list[dict[str, Any]] = []
    for i, h in enumerate(hits, start=1):
        context_blocks.append(
            {
                "rank": i,
                "source": h.source,
                "ref_id": h.ref_id,
                "pathology_group": h.pathology_group,
                "distance": h.distance,
                "excerpt": h.content,
            }
        )

    ctx_text = "\n\n".join(
        f"[{b['rank']}] ({b['source']} id={b['ref_id']}) pathology={b['pathology_group']}\n{b['excerpt']}"
        for b in context_blocks
    )

    prompt = f"""You are an expert musculoskeletal radiology assistant for an educational QA system.

Hard filters already applied to retrieval: use only the evidence below as primary support; if insufficient, say what is missing.

Pathology emphasis ({pathology or 'General'}): {hint}

Student question:
{user_question.strip()}

Retrieved evidence (ranked by vector similarity under hybrid metadata filters):
{ctx_text if ctx_text else '(no hits — answer from general principles only, and state uncertainty explicitly.)'}
"""

    return prompt, context_blocks


def rag_answer_prepare(
    conn: PGConnection,
    *,
    user_question: str,
    image_vector: np.ndarray | None,
    modality: str,
    anatomy: str,
    pathology_group: str | None = None,
    per_source_limit: int = 3,
    final_top_k: int = 5,
) -> dict[str, Any]:
    """Embed question, hybrid-retrieve, build prompt JSON for upstream LLM gateway."""
    q_vec = encode_text(user_question)
    hits = hybrid_retrieve(
        conn,
        user_question=user_question,
        query_text_vector=q_vec,
        image_vector=image_vector,
        modality=modality,
        anatomy=anatomy,
        pathology_group=pathology_group,
        per_source_limit=per_source_limit,
        final_top_k=final_top_k,
    )
    prompt, context = build_llm_prompt(
        user_question=user_question,
        hits=hits,
        dominant_pathology=pathology_group,
    )
    return {
        "prompt": prompt,
        "context": context,
        "retrieval_count": len(hits),
        "filters": {
            "modality": modality_for_db(modality),
            "anatomy": anatomy,
            "pathology_group": pathology_group,
        },
    }
