"""POST /ask — hybrid RAG retrieval + assembled prompt for C# gateway."""

from __future__ import annotations

import uuid

import numpy as np
from fastapi import APIRouter, HTTPException
from pydantic import BaseModel, Field

from app.core.db import get_connection
from app.services.embeddings import image_embedding_dim
from app.services.rag_service import rag_answer_prepare

router = APIRouter(tags=["qa"])


class AskBody(BaseModel):
    user_question: str = Field(..., min_length=1)
    modality: str = Field(..., description="Ontology or DB modality (e.g. X-ray / X-Ray)")
    anatomy: str = Field(..., description="Anatomy site or legacy region bucket (must match case_metadata)")
    pathology_group: str | None = Field(
        None,
        description="Optional strict pathology_group filter (must match case_metadata / chunks)",
    )
    image_embedding: list[float] | None = Field(
        None,
        description="Optional BiomedCLIP image vector (same space as case_media_embeddings.image_vector)",
    )
    case_id: uuid.UUID | None = Field(
        None,
        description="Catalog case id — when image_embedding is omitted, server loads stored image_vector",
    )
    case_media_id: uuid.UUID | None = Field(
        None,
        description="Optional case_media.id disambiguator when multiple series exist for the case",
    )
    dicom_clinical_context: str | None = Field(
        None,
        description="Pre-formatted DICOM tag block (Modality, body part, patient age/sex, etc.) for RAG prompt context",
    )


@router.post("/ask")
def ask(body: AskBody) -> dict:
    img_vec: np.ndarray | None = None
    if body.image_embedding is not None:
        arr = np.asarray(body.image_embedding, dtype=np.float32)
        expected = image_embedding_dim()
        if arr.size != expected:
            raise HTTPException(
                status_code=400,
                detail=f"image_embedding must be length {expected} (BiomedCLIP) when provided.",
            )
        img_vec = arr

    try:
        with get_connection() as conn:
            out = rag_answer_prepare(
                conn,
                user_question=body.user_question,
                image_vector=img_vec,
                modality=body.modality,
                anatomy=body.anatomy,
                pathology_group=body.pathology_group,
                case_id=body.case_id,
                case_media_id=body.case_media_id,
                dicom_clinical_context=body.dicom_clinical_context,
            )
    except RuntimeError as e:
        raise HTTPException(status_code=500, detail=str(e)) from e

    return out
