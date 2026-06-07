"""POST /ingest - DICOM path to ontology to real embeddings to Supabase."""

from __future__ import annotations

import math
import os
import re
import tempfile
import traceback
import zipfile
from pathlib import Path
from uuid import UUID, uuid4

import httpx
from fastapi import APIRouter, HTTPException
from pydantic import BaseModel, Field

from app.core.db import get_connection, insert_ingest_bundle
from app.services.dicom_reader import (
    DicomSourceError,
    build_case_dicom_metadata,
    extract_archive,
    extract_dicom_image,
    find_dicom_files,
    is_archive_path,
    is_remote_dicom_reference,
    local_dicom_path,
    read_dicom_tags,
    select_representative_dicom,
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
from app.services.ontology.anatomy_site import ensure_anatomy_site
from app.services.ontology.medical_metadata import (
    DIFFICULTIES,
    MODALITIES_DB,
    PATHOLOGY_GROUPS,
    SOURCE_TYPES,
    clinical_context_payload,
    clamp_quality,
    validate_enum,
)
from app.services.supabase_storage import storage_target_for_ingest, upload_png_file

router = APIRouter(tags=["ingest"])


def _raise_ingest_error(context: str, exc: Exception) -> None:
    traceback.print_exc()
    detail = f"{context}: {type(exc).__name__}: {exc}"
    if "Repo id must use alphanumeric" in str(exc) or "Failed initial config/weights load from HF Hub" in str(exc):
        detail = (
            "Image embedding model misconfigured on the AI service. "
            "Set IMAGE_EMBEDDING_MODEL to a Hugging Face repo id (org/model) or remove it; "
            "put your API token in HUGGINGFACE_API_KEY only."
        )
    raise HTTPException(status_code=500, detail=detail) from exc


class IngestBody(BaseModel):
    dicom_path: str = Field(
        ...,
        description="Local filesystem path or http(s) URL: single DICOM, or local .zip/.rar study archive",
    )
    diagnosis_text: str | None = Field(
        None,
        description="Optional clinical/diagnosis text supplied by the gateway (no server-side file lookup)",
    )
    modality: str | None = Field(None, description="Override: X-Ray, CT, MRI, Ultrasound")
    anatomy_site: str | None = Field(None, description="Override: Spine, Knee, …")
    laterality: str | None = Field(None, description="Override: Left, Right, Bilateral, Not-Applicable")
    view_position: str | None = Field(None, description="Override: AP, Lateral, Oblique, PA")
    pathology_group: str | None = Field(None, description="Override: Trauma, Degenerative, …")
    difficulty: str | None = Field(None, description="Override: Easy, Medium, Hard")
    source_type: str | None = Field(None, description="Clinical, Training, or Research")
    quality_score: float | None = Field(None, ge=0.0, le=1.0)
    clinical_evidence: str | None = Field(None, description="Structured clinical evidence text")
    ingest_purpose: str = Field(
        "library",
        description="library = expert case catalog; personal = student Visual QA upload",
    )
    owner_user_id: str | None = Field(
        None,
        description="Uploader user id (required for personal ingest storage paths)",
    )


def _normalize_laterality(raw: str | None) -> str:
    if not raw:
        return "Not-Applicable"
    u = raw.strip().upper().replace(" ", "")
    if u in ("L", "LEFT"):
        return "Left"
    if u in ("R", "RIGHT"):
        return "Right"
    if u in ("B", "BILATERAL", "BOTH"):
        return "Bilateral"
    return "Not-Applicable"


def _normalize_view_position(raw: str | None) -> str:
    if not raw:
        return "AP"
    u = raw.strip().upper()
    if u in ("PA",):
        return "PA"
    if u in ("LAT", "LATERAL", "LL", "RL"):
        return "Lateral"
    if u in ("OBL", "OBLIQUE"):
        return "Oblique"
    if u in ("AP", "FRONTAL"):
        return "AP"
    return "AP"


def _estimate_quality_from_pixels(rows: int | None, cols: int | None) -> float:
    r = max(0, int(rows or 0))
    c = max(0, int(cols or 0))
    px = r * c
    if px <= 0:
        return 0.45
    return float(min(1.0, max(0.35, math.sqrt(float(px)) / 1200.0)))


def _differentials_from_diagnosis(diagnosis: str) -> list[str]:
    if not diagnosis.strip():
        return []
    parts = [p.strip() for p in re.split(r"[,;|]", diagnosis) if p.strip()]
    return parts[:16]


def _ingest_from_file(body: IngestBody, store_path: str, dicom_file: Path) -> dict:
    tags = read_dicom_tags(dicom_file)
    patient_id = tags.get("patient_id")
    diagnosis = (body.diagnosis_text or "").strip()

    if body.modality:
        tier1 = validate_enum("modality", body.modality, MODALITIES_DB)
    else:
        tier1 = map_modality_tier1(str(tags.get("modality")) if tags.get("modality") else None)

    tier2 = infer_anatomy_from_text(diagnosis) or map_anatomy_tier2(
        str(tags.get("body_part_examined")) if tags.get("body_part_examined") else None
    )
    tier3 = validate_enum("pathology_group", body.pathology_group, PATHOLOGY_GROUPS) if body.pathology_group else map_pathology_tier3(diagnosis)

    anatomy_site = ensure_anatomy_site(
        body.anatomy_site,
        str(tags.get("body_part_examined")) if tags.get("body_part_examined") else None,
        diagnosis,
    )
    laterality = (
        validate_enum("laterality", body.laterality, frozenset({"Left", "Right", "Bilateral", "Not-Applicable"}))
        if body.laterality
        else _normalize_laterality(str(tags.get("laterality")) if tags.get("laterality") else None)
    )
    view_position = (
        validate_enum("view_position", body.view_position, frozenset({"AP", "Lateral", "Oblique", "PA"}))
        if body.view_position
        else _normalize_view_position(str(tags.get("view_position")) if tags.get("view_position") else None)
    )
    difficulty = (
        validate_enum("difficulty", body.difficulty, DIFFICULTIES) if body.difficulty else "Medium"
    )
    if body.ingest_purpose == "personal":
        source_type = "Training"
    else:
        source_type = (
            validate_enum("source_type", body.source_type, SOURCE_TYPES) if body.source_type else "Training"
        )
    qscore = (
        clamp_quality(body.quality_score)
        if body.quality_score is not None
        else _estimate_quality_from_pixels(
            int(tags["rows"]) if tags.get("rows") is not None else None,
            int(tags["columns"]) if tags.get("columns") is not None else None,
        )
    )

    diffs = _differentials_from_diagnosis(diagnosis)
    evidence = (body.clinical_evidence or diagnosis or "").strip() or None
    ctx = clinical_context_payload(
        source="bonevisqa-ai-ingest",
        differential_diagnoses=diffs if len(diffs) >= 2 else (diffs + [tier3]) if diffs else [tier3],
        clinical_evidence=evidence,
        extra={
            "tier2_region": tier2,
            "patient_id": patient_id,
            "ingest_purpose": body.ingest_purpose,
        },
    )

    text_for_embedding = diagnosis or f"Case {patient_id or 'unknown'} {tier1} {tier2} {tier3}"
    txt_vec = encode_text(text_for_embedding)
    image = extract_dicom_image(dicom_file)
    img_vec = encode_image(image)

    case_id = uuid4()
    media_id = uuid4()
    catalog_image_id = uuid4()
    store_p = Path(store_path).resolve()
    preview_dir = store_p if store_p.is_dir() else store_p.parent
    preview_name = f"bva_preview_{case_id}_{catalog_image_id}.png"
    preview_path = preview_dir / preview_name
    try:
        preview_dir.mkdir(parents=True, exist_ok=True)
        image.save(str(preview_path), format="PNG")
    except OSError:
        preview_root = Path(tempfile.gettempdir()) / "bonevisqa_medical_image_previews"
        preview_root.mkdir(parents=True, exist_ok=True)
        preview_path = preview_root / preview_name
        image.save(str(preview_path), format="PNG")

    purpose = (body.ingest_purpose or "library").strip().lower()
    if purpose not in ("library", "personal"):
        raise HTTPException(status_code=400, detail="ingest_purpose must be 'library' or 'personal'.")
    if purpose == "personal" and not (body.owner_user_id or "").strip():
        raise HTTPException(status_code=400, detail="owner_user_id is required for personal ingest.")

    bucket, object_path = storage_target_for_ingest(
        purpose,
        body.owner_user_id,
        str(case_id),
        str(catalog_image_id),
    )
    try:
        preview_public_url = upload_png_file(
            png_path=preview_path,
            bucket=bucket,
            object_path=object_path,
        )
    except Exception as e:
        _raise_ingest_error("Supabase preview upload failed", e)

    owner_student_id: UUID | None = None
    if purpose == "personal":
        owner_student_id = UUID((body.owner_user_id or "").strip())

    archive_hint = store_path if is_archive_path(store_path) else None
    dicom_metadata = build_case_dicom_metadata(
        tags,
        preview_url=preview_public_url,
        storage_path=object_path,
        anatomy_site=anatomy_site,
        laterality=laterality,
        view_position=view_position,
        quality_score=qscore,
        archive_path=archive_hint,
    )

    try:
        with get_connection() as conn:
            insert_ingest_bundle(
                conn,
                case_id=case_id,
                media_id=media_id,
                catalog_image_id=catalog_image_id,
                representative_raster_path=preview_public_url,
                preview_storage_path=object_path,
                dicom_metadata=dicom_metadata,
                tier1_modality=tier1,
                tier2_anatomy=tier2,
                tier3_pathology=tier3,
                diagnosis_text=diagnosis,
                image_vec=img_vec,
                text_vec=txt_vec,
                image_embedding_model=image_model_name(),
                anatomy_site=anatomy_site,
                laterality=laterality,
                view_position=view_position,
                difficulty=difficulty,
                source_type=source_type,
                quality_score=qscore,
                clinical_context=ctx,
                owner_student_id=owner_student_id,
            )
    except Exception as e:
        _raise_ingest_error("database insert failed", e)

    return {
        "case_id": str(case_id),
        "media_id": str(media_id),
        "catalog_image_id": str(catalog_image_id),
        "preview_image_url": preview_public_url,
        "dicom_metadata": dicom_metadata,
        "ingest_purpose": purpose,
        "patient_id": patient_id,
        "ontology": {
            "tier1_modality": tier1,
            "tier2_anatomy": tier2,
            "tier3_pathology": tier3,
            "anatomy_site": anatomy_site,
            "laterality": laterality,
            "view_position": view_position,
            "difficulty": difficulty,
            "source_type": source_type,
            "quality_score": qscore,
        },
        "diagnosis_used": diagnosis,
        "embedding_dim": int(img_vec.shape[0]),
        "models": {
            "text": text_model_name(),
            "image": image_model_name(),
        },
    }


def _ingest_local_archive(body: IngestBody, resolved_archive_path: str) -> dict:
    ap = Path(resolved_archive_path)
    if not ap.is_file():
        raise DicomSourceError(f"archive not found: {ap}")
    with tempfile.TemporaryDirectory(prefix="bonevisqa_ingest_") as tdir:
        extract_root = Path(tdir)
        extract_archive(ap, extract_root)
        dicom_files = find_dicom_files(extract_root)
        if not dicom_files:
            raise DicomSourceError("no DICOM files found in archive", status_code=400)
        chosen = select_representative_dicom(dicom_files)
        return _ingest_from_file(body, store_path=resolved_archive_path, dicom_file=chosen)


def _ingest_remote_archive(body: IngestBody, url: str) -> dict:
    """Download a remote .zip/.rar study and run the same extract pipeline as local archives."""
    suffix = Path(url.split("?", 1)[0]).suffix.lower() or ".zip"
    if suffix not in {".zip", ".rar"}:
        suffix = ".zip"
    with tempfile.NamedTemporaryFile(delete=False, suffix=suffix) as tmp:
        tmp_path = tmp.name
    try:
        with httpx.Client(timeout=httpx.Timeout(300.0, connect=30.0), follow_redirects=True) as client:
            with client.stream("GET", url) as response:
                response.raise_for_status()
                with open(tmp_path, "wb") as out:
                    for chunk in response.iter_bytes(chunk_size=1024 * 1024):
                        out.write(chunk)
        return _ingest_local_archive(body, tmp_path)
    finally:
        try:
            if os.path.isfile(tmp_path):
                os.unlink(tmp_path)
        except OSError:
            pass


@router.post("/ingest")
def ingest(body: IngestBody) -> dict:
    raw_path = body.dicom_path.strip()
    store_path = (
        raw_path if is_remote_dicom_reference(raw_path) else str(Path(raw_path).resolve())
    )

    try:
        if is_remote_dicom_reference(raw_path) and is_archive_path(raw_path):
            return _ingest_remote_archive(body, raw_path)

        if not is_remote_dicom_reference(raw_path) and is_archive_path(store_path):
            return _ingest_local_archive(body, store_path)

        with local_dicom_path(raw_path) as dp:
            return _ingest_from_file(body, store_path=store_path, dicom_file=Path(dp))
    except DicomSourceError as e:
        raise HTTPException(status_code=e.status_code, detail=str(e)) from e
    except HTTPException:
        raise
    except zipfile.BadZipFile as e:
        raise HTTPException(
            status_code=400,
            detail="Invalid DICOM archive: the ZIP file is corrupt or not a valid archive.",
        ) from e
    except OSError as e:
        raise HTTPException(
            status_code=400,
            detail=f"Invalid DICOM archive: could not read study file ({e}).",
        ) from e
    except Exception as e:
        _raise_ingest_error("ingest failed", e)
