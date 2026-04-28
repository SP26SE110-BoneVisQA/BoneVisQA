"""PostgreSQL (Supabase) inserts for multimodal RAG tables — parameterized SQL."""

from __future__ import annotations

import os
from contextlib import contextmanager
from typing import Generator
from uuid import UUID

import numpy as np
import psycopg2
from psycopg2.extensions import connection as PGConnection
from pgvector.psycopg2 import register_vector


def _database_url() -> str:
    url = os.environ.get("DATABASE_URL") or os.environ.get("SUPABASE_DB_URL")
    if not url:
        raise RuntimeError("Set DATABASE_URL or SUPABASE_DB_URL for Postgres.")
    return url


@contextmanager
def get_connection() -> Generator[PGConnection, None, None]:
    conn = psycopg2.connect(_database_url())
    register_vector(conn)
    try:
        yield conn
        conn.commit()
    except Exception:
        conn.rollback()
        raise
    finally:
        conn.close()


def modality_for_db(tier1_xray_ct_mri: str) -> str:
    """Align ontology tier-1 labels with common DB check constraints (X-Ray casing)."""
    if tier1_xray_ct_mri == "X-ray":
        return "X-Ray"
    return tier1_xray_ct_mri


def insert_ingest_bundle(
    conn: PGConnection,
    *,
    case_id: UUID,
    media_id: UUID,
    dicom_path: str,
    tier1_modality: str,
    tier2_anatomy: str,
    tier3_pathology: str,
    diagnosis_text: str,
    image_vec: np.ndarray,
    text_vec: np.ndarray,
) -> None:
    """Insert medical_cases row + case_metadata + case_media + embedding rows."""
    mod_db = modality_for_db(tier1_modality)

    with conn.cursor() as cur:
        cur.execute(
            """
            INSERT INTO public.medical_cases (
                id, title, description, difficulty,
                is_approved, is_active, indexing_status, version,
                created_at, updated_at
            )
            VALUES (
                %s, %s, %s, 'Medium',
                FALSE, TRUE, 'Completed', '1.0.0',
                NOW(), NOW()
            );
            """,
            (
                case_id,
                f"Ingested case {case_id}",
                diagnosis_text[:2000] if diagnosis_text else "(no diagnosis)",
            ),
        )

        cur.execute(
            """
            INSERT INTO public.case_metadata (
                case_id, modality, anatomy, pathology_group,
                suggested_diagnosis, clinical_context
            )
            VALUES (
                %s, %s, %s, %s,
                %s, %s::jsonb
            );
            """,
            (
                case_id,
                mod_db,
                tier2_anatomy,
                tier3_pathology,
                diagnosis_text or None,
                '{"source":"bonevisqa-ai-ingest"}',
            ),
        )

        cur.execute(
            """
            INSERT INTO public.case_media (
                id, case_id, media_url, storage_path, media_type,
                modality, anatomy, dicom_metadata
            )
            VALUES (
                %s, %s, %s, %s, 'DICOM',
                %s, %s, %s::jsonb
            );
            """,
            (
                media_id,
                case_id,
                dicom_path,
                dicom_path,
                mod_db,
                tier2_anatomy,
                '{"ingest":"ai-microservice"}',
            ),
        )

        cur.execute(
            """
            INSERT INTO public.case_media_embeddings (
                media_id, image_vector, embedding_model, embedding_dimensions
            )
            VALUES (
                %s, %s, 'clip-vit-base-patch32', %s
            );
            """,
            (media_id, image_vec, int(image_vec.shape[0])),
        )

        cur.execute(
            """
            INSERT INTO public.case_text_embeddings (
                case_id, source_text, source_type,
                text_vector, embedding_model, embedding_dimensions
            )
            VALUES (
                %s, %s, 'Diagnosis',
                %s, 'all-mpnet-base-v2', %s
            );
            """,
            (case_id, diagnosis_text or "", text_vec, int(text_vec.shape[0])),
        )
