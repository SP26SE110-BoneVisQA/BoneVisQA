"""FastAPI entrypoint for BoneVisQA AI microservice."""

from __future__ import annotations

import os
from pathlib import Path

from dotenv import load_dotenv

# Load .env before any module reads os.environ (db, Hugging Face, Supabase clients).
_ENV_FILE = Path(__file__).resolve().parents[1] / ".env"
load_dotenv(_ENV_FILE)
load_dotenv()

if _hf_key := os.environ.get("HUGGINGFACE_API_KEY"):
    os.environ.setdefault("HF_TOKEN", _hf_key)

import uvicorn
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from app.api.ingest import router as ingest_router
from app.api.v1.documents import router as documents_v1_router
from app.api.v1.qa import router as qa_v1_router

app = FastAPI(title="BoneVisQA AI", version="0.1.0")

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
    return {"status": "ok"}


if __name__ == "__main__":
    port = int(os.environ.get("PORT", "8000"))
    uvicorn.run("app.main:app", host="0.0.0.0", port=port)
