"""FastAPI entrypoint for BoneVisQA AI microservice."""

from __future__ import annotations

from fastapi import FastAPI

from app.api.ingest import router as ingest_router
from app.api.v1.qa import router as qa_v1_router

app = FastAPI(title="BoneVisQA AI", version="0.1.0")
app.include_router(ingest_router)
app.include_router(qa_v1_router, prefix="/api/v1/qa")


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}
