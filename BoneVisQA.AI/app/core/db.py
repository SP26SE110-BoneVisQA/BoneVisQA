"""PostgreSQL (Supabase) inserts for multimodal RAG tables — parameterized SQL."""

from __future__ import annotations

import json
import os
from contextlib import contextmanager
from pathlib import Path
from typing import Generator
from uuid import UUID

import numpy as np
import psycopg2
from dotenv import load_dotenv
from pgvector.psycopg2 import register_vector
from psycopg2.extensions import connection as PGConnection

# Ensure BoneVisQA.AI/.env is loaded even if this module is imported before app.main.
load_dotenv(Path(__file__).resolve().parents[2] / ".env")


def _database_url() -> str:
    url = (os.getenv("DATABASE_URL") or os.getenv("SUPABASE_DB_URL") or "").strip()
    if not url:
        raise RuntimeError("Set DATABASE_URL or SUPABASE_DB_URL for Postgres.")
    return url


@contextmanager
def get_connection() -> Generator[PGConnection, None, None]:
    conn = psycopg2.connect(_database_url(), connect_timeout=15)
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
    """Align ontology tier-1 labels with DB + C# filters (X-Ray casing)."""
    if tier1_xray_ct_mri == "X-ray":
        return "X-Ray"
    if tier1_xray_ct_mri == "Ultrasound":
        return "Ultrasound"
    return tier1_xray_ct_mri


_ALLOWED_MEDICAL_IMAGE_MODALITIES = frozenset({"X-Ray", "CT", "MRI", "Ultrasound", "Other"})


def modality_for_medical_images_check(mod_db: str) -> str:
    """Values allowed by medical_images_modality_check in PostgreSQL."""
    if mod_db in _ALLOWED_MEDICAL_IMAGE_MODALITIES:
        return mod_db
    return "Other"


def _fit_pgvector(vec: np.ndarray, *, expected_dim: int = 768) -> np.ndarray:
    """Match Supabase pgvector column width (BiomedCLIP is 512-d; columns are vector(768))."""
    v = np.asarray(vec, dtype=np.float32).flatten()
    if v.shape[0] == expected_dim:
        return v
    if v.shape[0] > expected_dim:
        raise ValueError(f"embedding length {v.shape[0]} exceeds column vector({expected_dim})")
    out = np.zeros(expected_dim, dtype=np.float32)
    out[: v.shape[0]] = v
    return out


def insert_ingest_bundle(
    conn: PGConnection,
    *,
    case_id: UUID,
    media_id: UUID,
    catalog_image_id: UUID,
    representative_raster_path: str,
    preview_storage_path: str,
    dicom_metadata: dict,
    tier1_modality: str,
    tier2_anatomy: str,
    tier3_pathology: str,
    diagnosis_text: str,
    image_vec: np.ndarray,
    text_vec: np.ndarray,
    image_embedding_model: str,
    anatomy_site: str,
    laterality: str,
    view_position: str,
    difficulty: str,
    source_type: str,
    quality_score: float,
    clinical_context: dict,
    owner_student_id: UUID | None = None,
) -> None:
    """Insert medical_cases row + case_metadata + case_media + medical_images + multimodal embedding rows."""
    mod_db = modality_for_db(tier1_modality)
    img_modality = modality_for_medical_images_check(mod_db)
    ctx_json = json.dumps(clinical_context, ensure_ascii=False)
    case_id_s, media_id_s, catalog_image_id_s = str(case_id), str(media_id), str(catalog_image_id)
    image_db = _fit_pgvector(image_vec)
    text_db = _fit_pgvector(text_vec)

    with conn.cursor() as cur:
        cur.execute(
            """
            INSERT INTO public.medical_cases (
                id, title, description, difficulty,
                is_approved, is_active, indexing_status, version,
                owner_student_id,
                created_at, updated_at
            )
            VALUES (
                %s::uuid, %s, %s, %s,
                FALSE, TRUE, 'Completed', '1.0.0',
                %s::uuid,
                NOW(), NOW()
            );
            """,
            (
                case_id_s,
                f"Ingested case {case_id}",
                diagnosis_text[:2000] if diagnosis_text else "(no diagnosis)",
                difficulty,
                str(owner_student_id) if owner_student_id is not None else None,
            ),
        )

        cur.execute(
            """
            INSERT INTO public.case_metadata (
                case_id, modality, anatomy, anatomy_site, pathology_group,
                laterality, view_position, difficulty, source_type, quality_score,
                suggested_diagnosis, clinical_context
            )
            VALUES (
                %s::uuid, %s, %s, %s, %s,
                %s, %s, %s, %s, %s,
                %s, %s::jsonb
            );
            """,
            (
                case_id_s,
                mod_db,
                tier2_anatomy,
                anatomy_site,
                tier3_pathology,
                laterality,
                view_position,
                difficulty,
                source_type,
                float(quality_score),
                diagnosis_text or None,
                ctx_json,
            ),
        )

        cur.execute(
            """
            INSERT INTO public.case_media (
                id, case_id, media_url, storage_path, media_type,
                modality, anatomy, dicom_metadata
            )
            VALUES (
                %s::uuid, %s::uuid, %s, %s, 'Image',
                %s, %s, %s::jsonb
            );
            """,
            (
                media_id_s,
                case_id_s,
                representative_raster_path,
                preview_storage_path,
                mod_db,
                tier2_anatomy,
                json.dumps(dicom_metadata, ensure_ascii=False),
            ),
        )

        cur.execute(
            """
            INSERT INTO public.medical_images (
                id, case_id, image_url, modality, created_at
            )
            VALUES (
                %s::uuid, %s::uuid, %s, %s, NOW()
            );
            """,
            (
                catalog_image_id_s,
                case_id_s,
                representative_raster_path,
                img_modality,
            ),
        )

        cur.execute(
            """
            INSERT INTO public.case_media_embeddings (
                media_id, image_vector, embedding_model, embedding_dimensions
            )
            VALUES (
                %s::uuid, %s, %s, %s
            );
            """,
            (media_id_s, image_db, image_embedding_model, int(image_db.shape[0])),
        )

        cur.execute(
            """
            INSERT INTO public.case_text_embeddings (
                case_id, source_text, source_type,
                text_vector, embedding_model, embedding_dimensions
            )
            VALUES (
                %s::uuid, %s, 'Diagnosis',
                %s, 'all-mpnet-base-v2', %s
            );
            """,
            (case_id_s, diagnosis_text or "", text_db, int(text_db.shape[0])),
        )
