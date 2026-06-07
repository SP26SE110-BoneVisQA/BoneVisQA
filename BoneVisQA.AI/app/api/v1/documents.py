"""Document chunk enrichment endpoints."""

from __future__ import annotations

import logging
import os
import time
from typing import Literal
from uuid import UUID

from fastapi import APIRouter, HTTPException
from pydantic import BaseModel, Field

from app.services.document_chunk_enrichment import enrich_document_chunks_by_id

router = APIRouter()
logger = logging.getLogger(__name__)

_DEFAULT_BATCH_SIZE = int(os.environ.get("ENRICH_BATCH_SIZE", "12"))
_DEFAULT_METADATA_BATCH_SIZE = int(os.environ.get("ENRICH_METADATA_BATCH_SIZE", "64"))


class EnrichChunksRequest(BaseModel):
    doc_id: UUID = Field(..., description="Document id whose chunks will be enriched")
    enrich_phase: Literal["metadata", "embeddings", "all"] = Field(
        "all",
        description="metadata = rule-based fields only; embeddings = vectors only; all = both",
    )
    only_missing_embedding: bool = Field(
        False,
        description="When true, only process chunks with null embedding (embeddings/all phases)",
    )
    batch_size: int | None = Field(
        None,
        ge=1,
        le=64,
        description="Max chunks per request; defaults by phase",
    )
    after_chunk_order: int = Field(
        -1,
        description="Process chunks with chunk_order greater than this cursor",
    )
    section_anatomy: str | None = Field(
        None,
        description="Section anatomy context carried from the previous batch",
    )
    section_pathology: str | None = Field(
        None,
        description="Section pathology context carried from the previous batch",
    )


class EnrichChunksResponse(BaseModel):
    doc_id: str
    chunks_processed: int
    embedding_model: str | None = None
    anatomy_distribution: dict[str, int]
    pathology_distribution: dict[str, int]
    null_embedding_remaining: int
    last_chunk_order: int
    section_anatomy: str | None = None
    section_pathology: str | None = None
    has_more: bool = False
    enrich_phase: str = "all"


@router.post("/enrich-chunks", response_model=EnrichChunksResponse)
def enrich_chunks(body: EnrichChunksRequest) -> EnrichChunksResponse:
    started = time.perf_counter()
    batch_size = body.batch_size
    if batch_size is None:
        batch_size = (
            _DEFAULT_METADATA_BATCH_SIZE
            if body.enrich_phase == "metadata"
            else _DEFAULT_BATCH_SIZE
        )

    logger.info(
        "enrich-chunks started doc_id=%s phase=%s batch=%s after_order=%s",
        body.doc_id,
        body.enrich_phase,
        batch_size,
        body.after_chunk_order,
    )
    try:
        result = enrich_document_chunks_by_id(
            body.doc_id,
            enrich_phase=body.enrich_phase,
            only_missing_embedding=body.only_missing_embedding,
            batch_size=batch_size,
            after_chunk_order=body.after_chunk_order,
            section_anatomy=body.section_anatomy,
            section_pathology=body.section_pathology,
        )
    except ValueError as exc:
        raise HTTPException(status_code=404, detail=str(exc)) from exc
    except Exception as exc:
        logger.exception("enrich-chunks failed doc_id=%s phase=%s", body.doc_id, body.enrich_phase)
        raise HTTPException(status_code=500, detail=str(exc)) from exc

    elapsed = time.perf_counter() - started
    logger.info(
        "enrich-chunks done doc_id=%s phase=%s chunks=%s null_remaining=%s has_more=%s elapsed=%.1fs",
        body.doc_id,
        result.get("enrich_phase"),
        result["chunks_processed"],
        result["null_embedding_remaining"],
        result["has_more"],
        elapsed,
    )
    return EnrichChunksResponse(**result)
