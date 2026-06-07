"""Embed document chunks and assign rule-based ontology metadata."""

from __future__ import annotations

import os
from collections import Counter
from typing import Any, Literal
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
from app.services.embeddings.text_encoder import encode_texts, text_model_name

_DEFAULT_BATCH_SIZE = int(os.environ.get("ENRICH_BATCH_SIZE", "40"))
_DEFAULT_METADATA_BATCH_SIZE = int(os.environ.get("ENRICH_METADATA_BATCH_SIZE", "64"))

EnrichPhase = Literal["metadata", "embeddings", "all"]


def _fit_pgvector(vec: np.ndarray, *, expected_dim: int = 768) -> np.ndarray:
    v = np.asarray(vec, dtype=np.float32).flatten()
    if v.shape[0] == expected_dim:
        return v
    if v.shape[0] > expected_dim:
        raise ValueError(f"embedding length {v.shape[0]} exceeds column vector({expected_dim})")
    out = np.zeros(expected_dim, dtype=np.float32)
    out[: v.shape[0]] = v
    return out


def _ensure_document_defaults(conn: PGConnection, doc_id: UUID) -> None:
    """Backfill legacy rows uploaded before default_modality existed."""
    with conn.cursor() as cur:
        cur.execute(
            """
            UPDATE public.documents
            SET default_modality = 'X-Ray'
            WHERE id = %s::uuid
              AND (default_modality IS NULL OR btrim(default_modality) = '');
            """,
            (str(doc_id),),
        )


def _load_document_defaults(conn: PGConnection, doc_id: UUID) -> tuple[str, str | None]:
    _ensure_document_defaults(conn, doc_id)
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


def _fetch_batch_rows(
    conn: PGConnection,
    *,
    doc_id: UUID,
    enrich_phase: EnrichPhase,
    only_missing_embedding: bool,
    after_chunk_order: int,
    batch_size: int,
) -> list[tuple[Any, str, int]]:
    with conn.cursor() as cur:
        if enrich_phase == "embeddings" or only_missing_embedding:
            cur.execute(
                """
                SELECT id, content, chunk_order
                FROM public.document_chunks
                WHERE doc_id = %s::uuid
                  AND embedding IS NULL
                  AND chunk_order > %s
                ORDER BY chunk_order
                LIMIT %s;
                """,
                (str(doc_id), after_chunk_order, batch_size),
            )
        else:
            cur.execute(
                """
                SELECT id, content, chunk_order
                FROM public.document_chunks
                WHERE doc_id = %s::uuid
                  AND chunk_order > %s
                ORDER BY chunk_order
                LIMIT %s;
                """,
                (str(doc_id), after_chunk_order, batch_size),
            )
        return cur.fetchall()


def _count_null_embeddings(conn: PGConnection, doc_id: UUID) -> int:
    with conn.cursor() as cur:
        cur.execute(
            """
            SELECT COUNT(*)::int
            FROM public.document_chunks
            WHERE doc_id = %s::uuid AND embedding IS NULL;
            """,
            (str(doc_id),),
        )
        return int(cur.fetchone()[0])


def enrich_document_chunks(
    conn: PGConnection,
    *,
    doc_id: UUID,
    enrich_phase: EnrichPhase = "all",
    only_missing_embedding: bool = False,
    batch_size: int | None = None,
    after_chunk_order: int = -1,
    section_anatomy: str | None = None,
    section_pathology: str | None = None,
) -> dict[str, Any]:
    phase: EnrichPhase = enrich_phase if enrich_phase in {"metadata", "embeddings", "all"} else "all"
    if batch_size is None:
        batch_size = _DEFAULT_METADATA_BATCH_SIZE if phase == "metadata" else _DEFAULT_BATCH_SIZE
    batch_size = max(1, min(int(batch_size), 64))

    default_modality, default_pathology = _load_document_defaults(conn, doc_id)
    rows = _fetch_batch_rows(
        conn,
        doc_id=doc_id,
        enrich_phase=phase,
        only_missing_embedding=only_missing_embedding,
        after_chunk_order=after_chunk_order,
        batch_size=batch_size,
    )

    if not rows:
        null_remaining = _count_null_embeddings(conn, doc_id)
        return {
            "doc_id": str(doc_id),
            "chunks_processed": 0,
            "embedding_model": text_model_name() if phase != "metadata" else None,
            "anatomy_distribution": {},
            "pathology_distribution": {},
            "null_embedding_remaining": null_remaining,
            "last_chunk_order": after_chunk_order,
            "section_anatomy": section_anatomy,
            "section_pathology": section_pathology,
            "has_more": False,
            "enrich_phase": phase,
        }

    anatomy_counts: Counter[str] = Counter()
    pathology_counts: Counter[str] = Counter()
    chunk_rows: list[tuple[Any, str, int]] = []
    metadata_by_id: dict[str, tuple[str, str, str]] = {}
    last_chunk_order = after_chunk_order

    run_metadata = phase in {"metadata", "all"}
    run_embeddings = phase in {"embeddings", "all"}

    for chunk_id, content, chunk_order in rows:
        text = str(content or "")
        modality = anatomy = pathology = "Other"

        if run_metadata:
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
            metadata_by_id[str(chunk_id)] = (modality, anatomy, pathology)
            anatomy_counts[anatomy] += 1
            pathology_counts[pathology] += 1

        chunk_rows.append((chunk_id, text, chunk_order))
        last_chunk_order = int(chunk_order)

    vectors: list[np.ndarray] = []
    if run_embeddings:
        texts = [text or " " for _, text, _ in chunk_rows]
        vectors = encode_texts(texts)

    processed = 0
    with conn.cursor() as cur:
        if run_metadata and run_embeddings:
            for (chunk_id, _text, _chunk_order), vec_raw in zip(chunk_rows, vectors, strict=True):
                modality, anatomy, pathology = metadata_by_id[str(chunk_id)]
                vec = _fit_pgvector(vec_raw)
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
                processed += 1
        elif run_metadata:
            for chunk_id, _text, _chunk_order in chunk_rows:
                modality, anatomy, pathology = metadata_by_id[str(chunk_id)]
                cur.execute(
                    """
                    UPDATE public.document_chunks
                    SET modality = %s,
                        anatomy = %s,
                        pathology_group = %s
                    WHERE id = %s::uuid;
                    """,
                    (modality, anatomy, pathology, str(chunk_id)),
                )
                processed += 1
        else:
            for (chunk_id, _text, _chunk_order), vec_raw in zip(chunk_rows, vectors, strict=True):
                vec = _fit_pgvector(vec_raw)
                cur.execute(
                    """
                    UPDATE public.document_chunks
                    SET embedding = %s
                    WHERE id = %s::uuid;
                    """,
                    (vec, str(chunk_id)),
                )
                processed += 1

    null_remaining = _count_null_embeddings(conn, doc_id)
    if phase == "metadata":
        has_more = processed >= batch_size
    elif phase == "embeddings" or only_missing_embedding:
        has_more = processed > 0 and null_remaining > 0
    else:
        has_more = processed > 0 and (
            (only_missing_embedding and null_remaining > 0)
            or (not only_missing_embedding and processed >= batch_size)
        )

    return {
        "doc_id": str(doc_id),
        "chunks_processed": processed,
        "embedding_model": text_model_name() if phase != "metadata" else None,
        "anatomy_distribution": dict(anatomy_counts),
        "pathology_distribution": dict(pathology_counts),
        "null_embedding_remaining": null_remaining,
        "last_chunk_order": last_chunk_order,
        "section_anatomy": section_anatomy,
        "section_pathology": section_pathology,
        "has_more": has_more,
        "enrich_phase": phase,
    }


def enrich_document_chunks_by_id(
    doc_id: UUID,
    *,
    enrich_phase: EnrichPhase = "all",
    only_missing_embedding: bool = False,
    batch_size: int | None = None,
    after_chunk_order: int = -1,
    section_anatomy: str | None = None,
    section_pathology: str | None = None,
) -> dict[str, Any]:
    with get_connection() as conn:
        return enrich_document_chunks(
            conn,
            doc_id=doc_id,
            enrich_phase=enrich_phase,
            only_missing_embedding=only_missing_embedding,
            batch_size=batch_size,
            after_chunk_order=after_chunk_order,
            section_anatomy=section_anatomy,
            section_pathology=section_pathology,
        )
