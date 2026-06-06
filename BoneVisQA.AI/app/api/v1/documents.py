"""Document chunk enrichment endpoints."""

from __future__ import annotations

from uuid import UUID

from fastapi import APIRouter, HTTPException
from pydantic import BaseModel, Field

from app.services.document_chunk_enrichment import enrich_document_chunks_by_id

router = APIRouter()


class EnrichChunksRequest(BaseModel):
    doc_id: UUID = Field(..., description="Document id whose chunks will be enriched")
    only_missing_embedding: bool = Field(
        False,
        description="When true, only process chunks with null embedding",
    )


class EnrichChunksResponse(BaseModel):
    doc_id: str
    chunks_processed: int
    embedding_model: str
    anatomy_distribution: dict[str, int]
    pathology_distribution: dict[str, int]
    null_embedding_remaining: int


@router.post("/enrich-chunks", response_model=EnrichChunksResponse)
def enrich_chunks(body: EnrichChunksRequest) -> EnrichChunksResponse:
    try:
        result = enrich_document_chunks_by_id(
            body.doc_id,
            only_missing_embedding=body.only_missing_embedding,
        )
    except ValueError as exc:
        raise HTTPException(status_code=404, detail=str(exc)) from exc
    except Exception as exc:
        raise HTTPException(status_code=500, detail=str(exc)) from exc

    return EnrichChunksResponse(**result)
