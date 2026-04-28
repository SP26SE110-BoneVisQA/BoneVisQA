"""POST /ask — hybrid RAG retrieval + assembled prompt for C# gateway."""

from __future__ import annotations

import numpy as np
from fastapi import APIRouter, HTTPException
from pydantic import BaseModel, Field

from app.core.db import get_connection
from app.services.rag_service import rag_answer_prepare

router = APIRouter(tags=["qa"])


class AskBody(BaseModel):
    user_question: str = Field(..., min_length=1)
    modality: str = Field(..., description="Ontology or DB modality (e.g. X-ray / X-Ray)")
    anatomy: str = Field(..., description="Tier-2 anatomy bucket")
    pathology_group: str | None = Field(
        None,
        description="Optional strict pathology_group filter (must match case_metadata / chunks)",
    )
    image_embedding: list[float] | None = Field(
        None,
        description="Optional 768-d image vector (same space as case_media_embeddings)",
    )


@router.post("/ask")
def ask(body: AskBody) -> dict:
    img_vec: np.ndarray | None = None
    if body.image_embedding is not None:
        arr = np.asarray(body.image_embedding, dtype=np.float32)
        if arr.size != 768:
            raise HTTPException(
                status_code=400,
                detail="image_embedding must be length 768 when provided.",
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
            )
    except RuntimeError as e:
        raise HTTPException(status_code=500, detail=str(e)) from e

    return out
