"""FastAPI entrypoint for BoneVisQA AI microservice."""

from __future__ import annotations

import asyncio
import logging
import os
from contextlib import asynccontextmanager
from pathlib import Path

from dotenv import load_dotenv

# Load .env before any module reads os.environ (db, Hugging Face, Supabase clients).
_ENV_FILE = Path(__file__).resolve().parents[1] / ".env"
load_dotenv(_ENV_FILE)
load_dotenv()

if _hf_key := os.environ.get("HUGGINGFACE_API_KEY"):
    os.environ.setdefault("HF_TOKEN", _hf_key)

import uvicorn
from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware

from app.api.ingest import router as ingest_router
from app.api.v1.documents import router as documents_v1_router
from app.api.v1.qa import router as qa_v1_router
from app.core.db import check_database_connection
from app.services.embeddings.image_encoder import image_model_name, warmup_image_model
from app.services.embeddings.text_encoder import text_model_name, warmup_text_model

logger = logging.getLogger(__name__)

_warmup_state = "skipped"  # pending | ready | failed | skipped
_warmup_error: str | None = None


def _run_embedding_warmup() -> None:
    global _warmup_state, _warmup_error
    try:
        logger.info("Pre-loading text embedding model (%s)...", text_model_name())
        warmup_text_model()
        logger.info("Text embedding model ready (%s).", text_model_name())

        logger.info("Pre-loading image embedding model (%s)...", image_model_name())
        warmup_image_model()
        logger.info("Image embedding model ready (%s).", image_model_name())
        _warmup_state = "ready"
    except Exception as exc:
        _warmup_state = "failed"
        _warmup_error = str(exc)
        logger.exception("Embedding warmup failed: %s", exc)


@asynccontextmanager
async def lifespan(_app: FastAPI):
    global _warmup_state
    warmup_task: asyncio.Future | None = None
    skip = os.environ.get("SKIP_EMBEDDING_WARMUP", "").strip().lower() in {"1", "true", "yes"}

    logger.info("BoneVisQA AI process starting (skip_warmup=%s).", skip)

    # Yield immediately so /health responds while models download in the background.
    if not skip:
        _warmup_state = "pending"
        loop = asyncio.get_running_loop()
        warmup_task = loop.run_in_executor(None, _run_embedding_warmup)

    yield

    if warmup_task is not None:
        try:
            await asyncio.wrap_future(warmup_task)
        except Exception:
            pass


app = FastAPI(title="BoneVisQA AI", version="0.1.0", lifespan=lifespan)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(ingest_router)
app.include_router(qa_v1_router, prefix="/api/v1/qa")
app.include_router(documents_v1_router, prefix="/api/v1/documents")


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok", "warmup": _warmup_state}


@app.get("/health/ready")
def health_ready() -> dict[str, str]:
    try:
        check_database_connection()
    except Exception as exc:
        raise HTTPException(status_code=503, detail=str(exc)) from exc

    if _warmup_state == "pending":
        raise HTTPException(
            status_code=503,
            detail="Embedding models are still loading. Retry shortly.",
        )
    if _warmup_state == "failed":
        raise HTTPException(
            status_code=503,
            detail=_warmup_error or "Embedding warmup failed.",
        )

    return {
        "status": "ready",
        "warmup": _warmup_state,
        "text_embedding_model": text_model_name(),
        "image_embedding_model": image_model_name(),
    }


if __name__ == "__main__":
    port = int(os.environ.get("PORT", "8000"))
    uvicorn.run("app.main:app", host="0.0.0.0", port=port)
