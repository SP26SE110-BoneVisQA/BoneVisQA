"""Document chunk enrichment endpoints."""

from __future__ import annotations

import logging
import time
from uuid import UUID

from fastapi import APIRouter, HTTPException
from pydantic import BaseModel, Field

from app.services.document_chunk_enrichment import enrich_document_chunks_by_id

router = APIRouter()
logger = logging.getLogger(__name__)


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
    started = time.perf_counter()
    logger.info(
        "enrich-chunks started doc_id=%s only_missing=%s",
        body.doc_id,
        body.only_missing_embedding,
    )
    try:
        result = enrich_document_chunks_by_id(
            body.doc_id,
            only_missing_embedding=body.only_missing_embedding,
        )
    except ValueError as exc:
        raise HTTPException(status_code=404, detail=str(exc)) from exc
    except Exception as exc:
        logger.exception("enrich-chunks failed doc_id=%s", body.doc_id)
        raise HTTPException(status_code=500, detail=str(exc)) from exc

    elapsed = time.perf_counter() - started
    logger.info(
        "enrich-chunks done doc_id=%s chunks=%s null_remaining=%s elapsed=%.1fs",
        body.doc_id,
        result["chunks_processed"],
        result["null_embedding_remaining"],
        elapsed,
    )
    return EnrichChunksResponse(**result)
