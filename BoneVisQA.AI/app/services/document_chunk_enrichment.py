"""Embed document chunks and assign rule-based ontology metadata."""

from __future__ import annotations

from collections import Counter
from typing import Any
from uuid import UUID

import numpy as np
from psycopg2.extensions import connection as PGConnection

from app.core.db import get_connection
from app.services.document_chunk_metadata import (
    first_heading_in_chunk,
    normalize_modality,
    resolve_chunk_metadata,
    section_metadata_from_heading,
)
from app.services.embeddings.text_encoder import encode_text, text_model_name


def _fit_pgvector(vec: np.ndarray, *, expected_dim: int = 768) -> np.ndarray:
    v = np.asarray(vec, dtype=np.float32).flatten()
    if v.shape[0] == expected_dim:
        return v
    if v.shape[0] > expected_dim:
        raise ValueError(f"embedding length {v.shape[0]} exceeds column vector({expected_dim})")
    out = np.zeros(expected_dim, dtype=np.float32)
    out[: v.shape[0]] = v
    return out


def _load_document_defaults(conn: PGConnection, doc_id: UUID) -> tuple[str, str | None]:
    with conn.cursor() as cur:
        cur.execute(
            """
            SELECT default_modality, default_pathology_group
            FROM public.documents
            WHERE id = %s::uuid;
            """,
            (str(doc_id),),
        )
        row = cur.fetchone()
        if not row:
            raise ValueError(f"Document {doc_id} not found.")
        default_modality = normalize_modality(row[0])
        default_pathology = row[1] if row[1] else None
        return default_modality, default_pathology


def enrich_document_chunks(
    conn: PGConnection,
    *,
    doc_id: UUID,
    only_missing_embedding: bool = False,
) -> dict[str, Any]:
    default_modality, default_pathology = _load_document_defaults(conn, doc_id)

    with conn.cursor() as cur:
        if only_missing_embedding:
            cur.execute(
                """
                SELECT id, content, chunk_order
                FROM public.document_chunks
                WHERE doc_id = %s::uuid
                  AND embedding IS NULL
                ORDER BY chunk_order;
                """,
                (str(doc_id),),
            )
        else:
            cur.execute(
                """
                SELECT id, content, chunk_order
                FROM public.document_chunks
                WHERE doc_id = %s::uuid
                ORDER BY chunk_order;
                """,
                (str(doc_id),),
            )
        rows = cur.fetchall()

    if not rows:
        return {
            "doc_id": str(doc_id),
            "chunks_processed": 0,
            "embedding_model": text_model_name(),
            "anatomy_distribution": {},
            "pathology_distribution": {},
            "null_embedding_remaining": 0,
        }

    section_anatomy: str | None = None
    section_pathology: str | None = None
    anatomy_counts: Counter[str] = Counter()
    pathology_counts: Counter[str] = Counter()
    processed = 0

    with conn.cursor() as cur:
        for chunk_id, content, _chunk_order in rows:
            text = str(content or "")
            heading = first_heading_in_chunk(text)
            if heading:
                sec_a, sec_p = section_metadata_from_heading(heading)
                if sec_a:
                    section_anatomy = sec_a
                if sec_p:
                    section_pathology = sec_p

            modality, anatomy, pathology = resolve_chunk_metadata(
                text,
                section_anatomy=section_anatomy,
                section_pathology=section_pathology,
                default_modality=default_modality,
                default_pathology=default_pathology,
            )
            vec = _fit_pgvector(encode_text(text))

            cur.execute(
                """
                UPDATE public.document_chunks
                SET modality = %s,
                    anatomy = %s,
                    pathology_group = %s,
                    embedding = %s
                WHERE id = %s::uuid;
                """,
                (modality, anatomy, pathology, vec, str(chunk_id)),
            )
            anatomy_counts[anatomy] += 1
            pathology_counts[pathology] += 1
            processed += 1

    with conn.cursor() as cur:
        cur.execute(
            """
            SELECT COUNT(*)::int
            FROM public.document_chunks
            WHERE doc_id = %s::uuid AND embedding IS NULL;
            """,
            (str(doc_id),),
        )
        null_remaining = int(cur.fetchone()[0])

    return {
        "doc_id": str(doc_id),
        "chunks_processed": processed,
        "embedding_model": text_model_name(),
        "anatomy_distribution": dict(anatomy_counts),
        "pathology_distribution": dict(pathology_counts),
        "null_embedding_remaining": null_remaining,
    }


def enrich_document_chunks_by_id(
    doc_id: UUID,
    *,
    only_missing_embedding: bool = False,
) -> dict[str, Any]:
    with get_connection() as conn:
        return enrich_document_chunks(
            conn,
            doc_id=doc_id,
            only_missing_embedding=only_missing_embedding,
        )
