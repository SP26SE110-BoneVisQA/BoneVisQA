"""Upload generated assets to Supabase Storage via REST API."""

from __future__ import annotations

import os
from pathlib import Path

import httpx


def _supabase_config() -> tuple[str, str]:
    base = (os.getenv("SUPABASE_URL") or "").rstrip("/")
    key = os.getenv("SUPABASE_SERVICE_KEY") or ""
    if not base or not key:
        raise RuntimeError("Set SUPABASE_URL and SUPABASE_SERVICE_KEY for Storage uploads.")
    return base, key


def upload_file_bytes(
    *,
    data: bytes,
    bucket: str,
    object_path: str,
    content_type: str = "application/octet-stream",
) -> str:
    """Upload bytes and return the public object URL."""
    base, key = _supabase_config()
    normalized = object_path.strip().replace("\\", "/").lstrip("/")
    upload_url = f"{base}/storage/v1/object/{bucket}/{normalized}"

    resp = httpx.post(
        upload_url,
        content=data,
        headers={
            "Authorization": f"Bearer {key}",
            "Content-Type": content_type,
            "x-upsert": "true",
        },
        timeout=120.0,
    )
    if resp.status_code >= 400:
        raise RuntimeError(
            f"Supabase storage upload failed ({resp.status_code}): {resp.text[:500]}"
        )

    return f"{base}/storage/v1/object/public/{bucket}/{normalized}"


def upload_png_file(*, png_path: Path, bucket: str, object_path: str) -> str:
    data = png_path.read_bytes()
    return upload_file_bytes(
        data=data,
        bucket=bucket,
        object_path=object_path,
        content_type="image/png",
    )


def storage_target_for_ingest(ingest_purpose: str, owner_user_id: str | None, case_id: str, image_id: str) -> tuple[str, str]:
    """Return (bucket, object_path) for preview PNG."""
    if ingest_purpose == "personal":
        owner = (owner_user_id or "anonymous").strip()
        bucket = "student_uploads"
        path = f"personal/{owner}/cases/{case_id}/preview_{image_id}.png"
    else:
        bucket = "medical-cases"
        path = f"ingest/{case_id}/preview_{image_id}.png"
    return bucket, path
