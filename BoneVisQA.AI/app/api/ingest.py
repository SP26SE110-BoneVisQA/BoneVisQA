"""POST /ingest - DICOM path to ontology to real embeddings to Supabase."""

from __future__ import annotations

from pathlib import Path
from uuid import uuid4

from fastapi import APIRouter, HTTPException
from pydantic import BaseModel, Field

from app.core.db import get_connection, insert_ingest_bundle
from app.services.dicom_reader import (
    DicomSourceError,
    extract_dicom_image,
    is_remote_dicom_reference,
    local_dicom_path,
    read_dicom_tags,
)
from app.services.embeddings import (
    encode_image,
    encode_text,
    image_model_name,
    text_model_name,
)
from app.services.ontology import (
    infer_anatomy_from_text,
    map_anatomy_tier2,
    map_modality_tier1,
    map_pathology_tier3,
)

router = APIRouter(tags=["ingest"])


class IngestBody(BaseModel):
    dicom_path: str = Field(
        ...,
        description="Local filesystem path or http(s) URL to a DICOM object",
    )
    diagnosis_text: str | None = Field(None, description="Optional; overrides chandoan lookup")
    chandoan_path: str | None = Field(
        None,
        description="Optional tab file: col1 = patient CH code, col2 = diagnosis",
    )


def _resolve_diagnosis(
    *,
    explicit: str | None,
    chandoan_path: str | None,
    patient_id: str | None,
) -> str:
    if explicit and explicit.strip():
        return explicit.strip()
    if not chandoan_path or not patient_id:
        return ""
    cp = Path(chandoan_path)
    if not cp.is_file():
        raise HTTPException(status_code=400, detail=f"chandoan_path not found: {cp}")
    for i, line in enumerate(cp.read_text(encoding="utf-8").splitlines()):
        if i == 0 and "Ma" in line:
            continue
        parts = line.split("\t", 1)
        if len(parts) == 2 and parts[0].strip() == patient_id:
            return parts[1].strip()
    return ""


@router.post("/ingest")
def ingest(body: IngestBody) -> dict:
    raw_path = body.dicom_path.strip()
    store_path = (
        raw_path if is_remote_dicom_reference(raw_path) else str(Path(raw_path).resolve())
    )

    try:
        with local_dicom_path(raw_path) as dp:
            tags = read_dicom_tags(dp)
            patient_id = tags.get("patient_id")
            diagnosis = _resolve_diagnosis(
                explicit=body.diagnosis_text,
                chandoan_path=body.chandoan_path,
                patient_id=patient_id,
            )

            tier1 = map_modality_tier1(tags.get("modality"))
            tier2 = infer_anatomy_from_text(diagnosis) or map_anatomy_tier2(
                tags.get("body_part_examined")
            )
            tier3 = map_pathology_tier3(diagnosis)

            text_for_embedding = diagnosis or f"Case {patient_id or 'unknown'} {tier1} {tier2} {tier3}"
            txt_vec = encode_text(text_for_embedding)
            image = extract_dicom_image(dp)
            img_vec = encode_image(image)

            case_id = uuid4()
            media_id = uuid4()

            try:
                with get_connection() as conn:
                    insert_ingest_bundle(
                        conn,
                        case_id=case_id,
                        media_id=media_id,
                        dicom_path=store_path,
                        tier1_modality=tier1,
                        tier2_anatomy=tier2,
                        tier3_pathology=tier3,
                        diagnosis_text=diagnosis,
                        image_vec=img_vec,
                        text_vec=txt_vec,
                    )
            except RuntimeError as e:
                raise HTTPException(status_code=500, detail=str(e)) from e
            except Exception as e:
                raise HTTPException(status_code=500, detail=f"database error: {e}") from e
    except DicomSourceError as e:
        raise HTTPException(status_code=e.status_code, detail=str(e)) from e

    return {
        "case_id": str(case_id),
        "media_id": str(media_id),
        "patient_id": patient_id,
        "ontology": {
            "tier1_modality": tier1,
            "tier2_anatomy": tier2,
            "tier3_pathology": tier3,
        },
        "diagnosis_used": diagnosis,
        "embedding_dim": int(img_vec.shape[0]),
        "models": {
            "text": text_model_name(),
            "image": image_model_name(),
        },
    }
