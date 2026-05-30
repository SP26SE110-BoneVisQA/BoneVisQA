"""Hybrid RAG retrieval with late fusion (text + image + metadata) and prompt assembly."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any
from uuid import UUID

import numpy as np
from psycopg2.extensions import connection as PGConnection

from app.core.db import modality_for_db
from app.services.embeddings.text_encoder import encode_text

ALPHA_FUSION = 0.45
BETA_FUSION = 0.45
GAMMA_FUSION = 0.10


@dataclass(frozen=True)
class RetrievalHit:
    source: str
    ref_id: str
    content: str
    pathology_group: str | None
    distance: float
    fusion_score: float
    text_similarity: float
    image_similarity: float
    metadata_score: float


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
    "Infection": (
        "Focus on infectious and inflammatory mimics, septic features, marrow signal patterns, "
        "and urgent surgical indications when suspected."
    ),
    "Congenital": (
        "Focus on developmental morphology, growth plates, dysplasia patterns, "
        "and age-appropriate norms."
    ),
}


def _pathology_instruction(pathology_group: str | None) -> str:
    if not pathology_group:
        return "Use general musculoskeletal radiology reasoning."
    pg = pathology_group
    if pg == "Inflammation":
        pg = "Infection"
    return _PATHOLOGY_PROMPT_HINTS.get(
        pg,
        "Use general musculoskeletal radiology reasoning.",
    )


def _cosine_sim_from_distance(dist: float) -> float:
    """pgvector cosine distance `<=>` on L2-normalized vectors → cosine similarity ≈ 1 - d."""
    return float(max(0.0, min(1.0, 1.0 - float(dist))))


def _effective_fusion_weights(has_query_image: bool) -> tuple[float, float, float]:
    """If no query image vector, drop the image branch and renormalize alpha/gamma."""
    if has_query_image:
        return ALPHA_FUSION, BETA_FUSION, GAMMA_FUSION
    denom = ALPHA_FUSION + GAMMA_FUSION
    if denom <= 0:
        return 1.0, 0.0, 0.0
    return ALPHA_FUSION / denom, 0.0, GAMMA_FUSION / denom


def _case_metadata_match_score(
    *,
    cm_mod: str,
    cm_ana: str,
    cm_pg: str | None,
    filter_mod: str,
    filter_ana: str,
    filter_pg: str | None,
) -> float:
    parts: list[float] = []
    parts.append(1.0 if cm_mod == filter_mod else 0.0)
    parts.append(1.0 if cm_ana == filter_ana else 0.0)
    if filter_pg is None:
        parts.append(1.0)
    else:
        parts.append(1.0 if (cm_pg or "") == filter_pg else 0.0)
    return sum(parts) / len(parts)


def fetch_case_image_vector(
    conn: PGConnection,
    *,
    case_id: UUID,
    media_id: UUID | None = None,
) -> np.ndarray | None:
    """Load BiomedCLIP image_vector for a catalog case (first media row if media_id not set)."""
    with conn.cursor() as cur:
        if media_id is None:
            cur.execute(
                """
                SELECT cme.image_vector
                FROM public.case_media_embeddings AS cme
                INNER JOIN public.case_media AS m ON m.id = cme.media_id
                WHERE m.case_id = %s::uuid
                  AND cme.image_vector IS NOT NULL
                ORDER BY m.id
                LIMIT 1;
                """,
                (str(case_id),),
            )
        else:
            cur.execute(
                """
                SELECT cme.image_vector
                FROM public.case_media_embeddings AS cme
                INNER JOIN public.case_media AS m ON m.id = cme.media_id
                WHERE m.case_id = %s::uuid
                  AND m.id = %s::uuid
                  AND cme.image_vector IS NOT NULL
                LIMIT 1;
                """,
                (str(case_id), str(media_id)),
            )
        row = cur.fetchone()
        if not row or row[0] is None:
            return None
        return np.asarray(row[0], dtype=np.float32)


def hybrid_retrieve(
    conn: PGConnection,
    *,
    user_question: str,
    query_text_vector: np.ndarray,
    image_vector: np.ndarray | None,
    modality: str,
    anatomy: str,
    pathology_group: str | None = None,
    per_source_limit: int = 8,
    final_top_k: int = 5,
) -> list[RetrievalHit]:
    """
    Late fusion over catalog cases and document chunks:
    Final = alpha * sim(text_q, case_text) + beta * sim(image_q, case_image) + gamma * metadata_match
    """
    mod = modality_for_db(modality)
    ana = anatomy
    pg = pathology_group
    qvec = query_text_vector.astype(np.float32)
    has_q_image = image_vector is not None
    alpha, beta, gamma = _effective_fusion_weights(has_q_image)
    ivec = image_vector.astype(np.float32) if image_vector is not None else None

    scored_rows: list[tuple[str, str, str, str | None, float, float, float, float]] = []

    with conn.cursor() as cur:
        cur.execute(
            """
            SELECT cte.case_id::text,
                   COALESCE(cte.source_text, ''),
                   cm.modality,
                   COALESCE(NULLIF(btrim(cm.anatomy_site), ''), cm.anatomy) AS ana,
                   cm.pathology_group,
                   (cte.text_vector <=> %(q)s::vector)::float8 AS text_dist
            FROM public.case_text_embeddings AS cte
            INNER JOIN public.case_metadata AS cm ON cm.case_id = cte.case_id
            INNER JOIN public.medical_cases AS mc ON mc.id = cte.case_id AND mc.is_approved = TRUE
            WHERE cte.text_vector IS NOT NULL
              AND cm.modality = %(mod)s
              AND COALESCE(NULLIF(btrim(cm.anatomy_site), ''), cm.anatomy) = %(ana)s
              AND (%(pg)s::text IS NULL OR cm.pathology_group = %(pg)s)
            ORDER BY cte.text_vector <=> %(q)s::vector
            LIMIT %(lim)s;
            """,
            {"q": qvec, "mod": mod, "ana": ana, "pg": pg, "lim": per_source_limit},
        )
        text_rows = cur.fetchall()

        image_by_case: dict[str, float] = {}
        if ivec is not None:
            cur.execute(
                """
                SELECT m.case_id::text,
                       (cme.image_vector <=> %(iv)s::vector)::float8 AS image_dist
                FROM public.case_media_embeddings AS cme
                INNER JOIN public.case_media AS m ON m.id = cme.media_id
                INNER JOIN public.case_metadata AS cm ON cm.case_id = m.case_id
                INNER JOIN public.medical_cases AS mc ON mc.id = m.case_id AND mc.is_approved = TRUE
                WHERE cme.image_vector IS NOT NULL
                  AND cm.modality = %(mod)s
                  AND COALESCE(NULLIF(btrim(cm.anatomy_site), ''), cm.anatomy) = %(ana)s
                  AND (%(pg)s::text IS NULL OR cm.pathology_group = %(pg)s)
                ORDER BY cme.image_vector <=> %(iv)s::vector
                LIMIT %(lim)s;
                """,
                {"iv": ivec, "mod": mod, "ana": ana, "pg": pg, "lim": per_source_limit},
            )
            for cid, dist in cur.fetchall():
                image_by_case[cid] = _cosine_sim_from_distance(float(dist))

        for row in text_rows:
            cid, content, cm_mod, cm_ana, cm_pg, text_dist = row
            text_sim = _cosine_sim_from_distance(float(text_dist))
            image_sim = image_by_case.get(cid, 0.0) if ivec is not None else 0.0
            meta = _case_metadata_match_score(
                cm_mod=str(cm_mod),
                cm_ana=str(cm_ana),
                cm_pg=str(cm_pg) if cm_pg is not None else None,
                filter_mod=mod,
                filter_ana=ana,
                filter_pg=pg,
            )
            fusion = alpha * text_sim + beta * image_sim + gamma * meta
            dist_for_client = max(0.0, min(2.0, 2.0 * (1.0 - fusion)))
            scored_rows.append(
                (
                    "case_text",
                    cid,
                    str(content)[:8000],
                    str(cm_pg) if cm_pg is not None else None,
                    dist_for_client,
                    fusion,
                    text_sim,
                    image_sim,
                    meta,
                )
            )

        cur.execute(
            """
            SELECT dc.id::text,
                   dc.content,
                   dc.modality,
                   dc.anatomy,
                   dc.pathology_group,
                   (dc.embedding <=> %(q)s::vector)::float8 AS text_dist
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
            rid, content, dc_mod, dc_ana, dc_pg, text_dist = row
            text_sim = _cosine_sim_from_distance(float(text_dist))
            image_sim = 0.0
            meta = _case_metadata_match_score(
                cm_mod=str(dc_mod),
                cm_ana=str(dc_ana),
                cm_pg=str(dc_pg) if dc_pg is not None else None,
                filter_mod=mod,
                filter_ana=ana,
                filter_pg=pg,
            )
            fusion = alpha * text_sim + beta * image_sim + gamma * meta
            dist_for_client = max(0.0, min(2.0, 2.0 * (1.0 - fusion)))
            scored_rows.append(
                (
                    "doc_chunk",
                    rid,
                    str(content)[:8000],
                    str(dc_pg) if dc_pg is not None else None,
                    dist_for_client,
                    fusion,
                    text_sim,
                    image_sim,
                    meta,
                )
            )

    scored_rows.sort(key=lambda r: r[4], reverse=False)
    hits: list[RetrievalHit] = []
    seen: set[tuple[str, str]] = set()
    for src, ref, content, path, dist, fusion, ts, ims, ms in scored_rows:
        key = (src, ref)
        if key in seen:
            continue
        seen.add(key)
        hits.append(
            RetrievalHit(
                source=src,
                ref_id=ref,
                content=content,
                pathology_group=path,
                distance=float(dist),
                fusion_score=float(fusion),
                text_similarity=float(ts),
                image_similarity=float(ims),
                metadata_score=float(ms),
            )
        )
        if len(hits) >= final_top_k:
            break

    return hits[:final_top_k]


def build_llm_prompt(
    *,
    user_question: str,
    hits: list[RetrievalHit],
    dominant_pathology: str | None,
    dicom_clinical_context: str | None = None,
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
                "fusion_score": h.fusion_score,
                "text_similarity": h.text_similarity,
                "image_similarity": h.image_similarity,
                "metadata_score": h.metadata_score,
                "excerpt": h.content,
            }
        )

    ctx_text = "\n\n".join(
        f"[{b['rank']}] ({b['source']} id={b['ref_id']}) pathology={b['pathology_group']} "
        f"fusion={b['fusion_score']:.4f} (text={b['text_similarity']:.4f}, image={b['image_similarity']:.4f}, meta={b['metadata_score']:.4f})\n"
        f"{b['excerpt']}"
        for b in context_blocks
    )

    dicom_block = ""
    if dicom_clinical_context and dicom_clinical_context.strip():
        dicom_block = f"\n{dicom_clinical_context.strip()}\n"

    prompt = f"""You are an expert musculoskeletal radiology assistant for an educational QA system.

Hard filters already applied to retrieval: use only the evidence below as primary support; if insufficient, say what is missing.

Pathology emphasis ({pathology or 'General'}): {hint}
{dicom_block}
Student question:
{user_question.strip()}

Retrieved evidence (late fusion: text + image + metadata under hybrid filters):
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
    case_id: UUID | None = None,
    case_media_id: UUID | None = None,
    dicom_clinical_context: str | None = None,
    per_source_limit: int = 8,
    final_top_k: int = 5,
) -> dict[str, Any]:
    """Embed question, late-fusion hybrid retrieve, build prompt JSON for upstream LLM gateway."""
    q_vec = encode_text(user_question)
    img_vec = image_vector
    if img_vec is None and case_id is not None:
        img_vec = fetch_case_image_vector(conn, case_id=case_id, media_id=case_media_id)

    hits = hybrid_retrieve(
        conn,
        user_question=user_question,
        query_text_vector=q_vec,
        image_vector=img_vec,
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
        dicom_clinical_context=dicom_clinical_context,
    )
    return {
        "prompt": prompt,
        "context": context,
        "retrieval_count": len(hits),
        "filters": {
            "modality": modality_for_db(modality),
            "anatomy": anatomy,
            "pathology_group": pathology_group,
            "case_id": str(case_id) if case_id else None,
            "late_fusion_weights": {
                "alpha": ALPHA_FUSION,
                "beta": BETA_FUSION,
                "gamma": GAMMA_FUSION,
                "query_image_resolved": img_vec is not None,
            },
        },
    }
