"""Load key tags and pixel data from DICOM via pydicom."""

from __future__ import annotations

import os
import shutil
import tempfile
import zipfile
from collections import defaultdict
from contextlib import contextmanager
from pathlib import Path
from typing import Any

import httpx
import numpy as np
import pydicom
import pydicom.misc
from pydicom.dataset import FileMetaDataset
from pydicom.pixel_data_handlers.util import apply_voi_lut
from pydicom.uid import ExplicitVRBigEndian, ExplicitVRLittleEndian, ImplicitVRLittleEndian, UID
from PIL import Image

# Enable JPEG/JPEG-LS/RLE decoders when optional wheels are installed.
for _handler_mod in (
    "pydicom.pixel_data_handlers.pylibjpeg_handler",
    "pydicom.pixel_data_handlers.gdcm_handler",
):
    try:
        __import__(_handler_mod)
    except ImportError:
        pass

try:
    import rarfile
except ImportError:  # pragma: no cover
    rarfile = None  # type: ignore[misc, assignment]


def is_remote_dicom_reference(dicom_path: str) -> bool:
    """True if ``dicom_path`` is an HTTP(S) URL (case-insensitive scheme)."""
    s = (dicom_path or "").strip().lower()
    return s.startswith("http://") or s.startswith("https://")


def is_archive_path(path: str | Path) -> bool:
    """True if the path looks like a study archive we extract in Python (``.zip`` / ``.rar``)."""
    return Path(path).suffix.lower() in {".zip", ".rar"}


def _is_within_directory(candidate: Path, directory: Path) -> bool:
    """Anti zip-slip: resolved ``candidate`` must lie under resolved ``directory``."""
    try:
        candidate.resolve().relative_to(directory.resolve())
        return True
    except ValueError:
        return False


def extract_archive(archive_path: str | Path, dest_dir: str | Path) -> None:
    """
    Extract a ZIP or RAR study archive into ``dest_dir`` (must be empty or dedicated temp tree).

    RAR: requires a working UnRAR binary (WinRAR's ``UnRAR.exe`` or ``unrar`` on PATH — see ``rarfile`` docs).
    """
    arc = Path(archive_path)
    dest = Path(dest_dir)
    if not arc.is_file():
        raise DicomSourceError(f"archive not found: {arc}")
    dest.mkdir(parents=True, exist_ok=True)
    dest_root = dest.resolve()
    suffix = arc.suffix.lower()

    if suffix == ".zip":
        if not zipfile.is_zipfile(arc):
            raise DicomSourceError(f"not a valid ZIP file: {arc}")
        with zipfile.ZipFile(arc) as zf:
            for info in zf.infolist():
                if info.is_dir():
                    continue
                target = (dest_root / info.filename).resolve()
                if not _is_within_directory(target, dest_root):
                    raise DicomSourceError("refusing ZIP path outside archive (zip slip)")
                target.parent.mkdir(parents=True, exist_ok=True)
                with zf.open(info, "r") as src, open(target, "wb") as dst:
                    shutil.copyfileobj(src, dst)
        return

    if suffix == ".rar":
        if rarfile is None:
            raise DicomSourceError(
                "rarfile is not installed; add it to requirements or use ZIP",
                status_code=500,
            )
        if not rarfile.is_rarfile(arc):
            raise DicomSourceError(f"not a valid RAR file: {arc}")
        with rarfile.RarFile(arc) as rf:
            for info in rf.infolist():
                if info.isdir():
                    continue
                target = (dest_root / info.filename).resolve()
                if not _is_within_directory(target, dest_root):
                    raise DicomSourceError("refusing RAR path outside archive")
                target.parent.mkdir(parents=True, exist_ok=True)
                rf.extract(info, path=str(dest_root))
        return

    raise DicomSourceError(f"unsupported archive type: {arc}")


_JUNK_SUFFIXES = frozenset({".txt", ".md", ".csv", ".json", ".xml", ".html", ".htm"})
_JUNK_NAMES = frozenset({"thumbs.db", ".ds_store"})
_SKIP_DIR_NAMES = frozenset({"__macosx", "__pycache__", ".git"})
# Storage Directory SOP Class UID — not a displayable image instance.
_STORAGE_DIRECTORY_SOP_CLASS = "1.2.840.10008.1.3.10"


def _looks_like_dicom(path: Path) -> bool:
    """Detect DICOM with or without a ``.dcm`` extension (common in study ZIPs)."""
    if not path.is_file() or path.stat().st_size < 132:
        return False
    try:
        if pydicom.misc.is_dicom(path):
            return True
    except Exception:
        pass
    try:
        pydicom.dcmread(str(path), stop_before_pixels=True, force=True)
        return True
    except Exception:
        return False


def _has_displayable_pixel_data(path: Path) -> bool:
    """Exclude DICOMDIR and directory records; require Rows×Columns or multi-frame pixel data."""
    upper_name = path.name.upper()
    if upper_name == "DICOMDIR" or upper_name.endswith("DICOMDIR"):
        return False
    try:
        ds = pydicom.dcmread(str(path), stop_before_pixels=True, force=True)
    except Exception:
        return False
    sop = str(getattr(ds, "SOPClassUID", "") or "").strip()
    if sop == _STORAGE_DIRECTORY_SOP_CLASS:
        return False
    rows = int(ds.get("Rows", 0) or 0)
    cols = int(ds.get("Columns", 0) or 0)
    if rows > 0 and cols > 0:
        return True
    nf = int(ds.get("NumberOfFrames", 0) or 0)
    return nf > 0


def find_dicom_files(root: str | Path) -> list[Path]:
    """Walk ``root`` for pydicom-readable DICOM objects; skip obvious junk (``.txt``, etc.)."""
    base = Path(root)
    out: list[Path] = []
    seen: set[str] = set()
    for dirpath, dirnames, filenames in os.walk(base):
        dirnames[:] = [
            d
            for d in dirnames
            if d.lower() not in _SKIP_DIR_NAMES and not d.startswith(".")
        ]
        for name in filenames:
            lower = name.lower()
            if lower.endswith(tuple(_JUNK_SUFFIXES)):
                continue
            if lower in _JUNK_NAMES or lower.startswith("._"):
                continue
            p = Path(dirpath) / name
            key = str(p.resolve())
            if key in seen:
                continue
            try:
                if _looks_like_dicom(p) and _has_displayable_pixel_data(p):
                    seen.add(key)
                    out.append(p)
            except OSError:
                continue
    return out


def _series_sort_metadata(path: Path) -> tuple[str, int, int, str] | None:
    """SeriesInstanceUID, pixel volume, sortable InstanceNumber, name for tie-break."""
    try:
        ds = pydicom.dcmread(str(path), stop_before_pixels=True)
    except Exception:
        return None
    suid_el = ds.get("SeriesInstanceUID")
    series_uid = str(suid_el).strip() if suid_el is not None else ""
    if not series_uid:
        series_uid = "__missing_series_uid__"
    rows = int(ds.get("Rows", 0) or 0)
    cols = int(ds.get("Columns", 0) or 0)
    nf = int(ds.get("NumberOfFrames", 1) or 1)
    if nf < 1:
        nf = 1
    pixels = rows * cols * nf
    inst = ds.get("InstanceNumber")
    try:
        inst_i = int(inst) if inst is not None else 10**9
    except (TypeError, ValueError):
        inst_i = 10**9
    return (series_uid, pixels, inst_i, path.name.lower())


def select_representative_dicom(paths: list[Path]) -> Path:
    """
    Pick one instance: group by SeriesInstanceUID, take the largest series by total pixel volume
    (rows×cols×frames summed over instances), tie-break by instance count; within the series take
    the geometric-middle slice by InstanceNumber (then path name).
    """
    if not paths:
        raise DicomSourceError("no DICOM paths to select from", status_code=400)

    series_map: dict[str, list[tuple[Path, int, int, str]]] = defaultdict(list)
    for p in paths:
        meta = _series_sort_metadata(p)
        if meta is None:
            continue
        suid, pixels, inst_i, name_l = meta
        series_map[suid].append((p, pixels, inst_i, name_l))

    if not series_map:
        raise DicomSourceError(
            "no readable DICOM metadata in archive (all candidates failed)",
            status_code=400,
        )

    def series_score(suid: str) -> tuple[int, int]:
        items = series_map[suid]
        return (sum(x[1] for x in items), len(items))

    best_uid = max(series_map.keys(), key=lambda k: series_score(k))
    items = series_map[best_uid]
    items.sort(key=lambda t: (t[2], t[3], str(t[0])))
    mid = len(items) // 2
    return items[mid][0]


class DicomSourceError(Exception):
    """Invalid local path or remote DICOM could not be downloaded."""

    def __init__(
        self,
        message: str,
        *,
        status_code: int = 400,
        original: Exception | None = None,
    ) -> None:
        self.status_code = status_code
        self.original = original
        super().__init__(message)


@contextmanager
def local_dicom_path(dicom_path: str):
    """
    Yield a ``Path`` to a readable DICOM file.

    Local paths are used as-is. Remote URLs are streamed to a temporary file,
    then the temp file is removed after processing (even if decoding fails).
    """
    s = (dicom_path or "").strip()
    if not s:
        raise DicomSourceError("dicom_path is empty")

    if not is_remote_dicom_reference(s):
        lp = Path(s)
        if not lp.is_file():
            raise DicomSourceError(f"dicom_path not found: {lp}")
        yield lp
        return

    tmp_path: str | None = None
    try:
        with httpx.Client(
            timeout=httpx.Timeout(120.0, connect=30.0),
            follow_redirects=True,
        ) as client:
            try:
                with client.stream("GET", s) as response:
                    response.raise_for_status()
                    with tempfile.NamedTemporaryFile(delete=False, suffix=".dcm") as tmp:
                        tmp_path = tmp.name
                        for chunk in response.iter_bytes(chunk_size=1024 * 1024):
                            tmp.write(chunk)
                        tmp.flush()
            except httpx.HTTPStatusError as e:
                sc = e.response.status_code
                gateway = 502 if sc >= 500 else 400
                raise DicomSourceError(
                    f"DICOM URL returned HTTP {sc} for {s}",
                    status_code=gateway,
                    original=e,
                ) from e
            except httpx.RequestError as e:
                raise DicomSourceError(
                    f"Failed to download DICOM: {e}",
                    status_code=502,
                    original=e,
                ) from e

        lp = Path(tmp_path) if tmp_path else None
        if lp is None or not lp.is_file() or lp.stat().st_size == 0:
            raise DicomSourceError("Downloaded DICOM file is missing or empty")
        yield lp
    finally:
        if tmp_path is not None:
            try:
                if os.path.isfile(tmp_path):
                    os.unlink(tmp_path)
            except OSError:
                pass


def _normalize_to_uint8(arr: np.ndarray) -> np.ndarray:
    arr = arr.astype(np.float32)
    lo = float(arr.min())
    hi = float(arr.max())
    if hi <= lo:
        return np.zeros_like(arr, dtype=np.uint8)
    arr = (arr - lo) / (hi - lo)
    arr = np.clip(arr * 255.0, 0.0, 255.0)
    return arr.astype(np.uint8)


def _tag_str(ds: pydicom.Dataset, name: str) -> str | None:
    val = ds.get(name)
    if val is None:
        return None
    s = str(val).strip()
    return s or None


def _tag_float(ds: pydicom.Dataset, name: str) -> float | None:
    val = ds.get(name)
    if val is None:
        return None
    try:
        return float(val)
    except (TypeError, ValueError):
        return None


def _tag_int(ds: pydicom.Dataset, name: str) -> int | None:
    val = ds.get(name)
    if val is None:
        return None
    try:
        return int(val)
    except (TypeError, ValueError):
        return None


def _infer_transfer_syntax_uid(ds: pydicom.Dataset) -> str | None:
    """Best-effort transfer syntax for malformed DICOMs missing file meta."""
    file_meta = getattr(ds, "file_meta", None)
    existing = getattr(file_meta, "TransferSyntaxUID", None) if file_meta is not None else None
    if existing:
        return str(existing)

    is_little_endian = getattr(ds, "is_little_endian", None)
    is_implicit_vr = getattr(ds, "is_implicit_VR", None)

    if is_little_endian is False:
        if is_implicit_vr is False:
            return str(ExplicitVRBigEndian)
        return None

    if is_implicit_vr is False:
        return str(ExplicitVRLittleEndian)

    if is_implicit_vr is True or is_little_endian is True:
        return str(ImplicitVRLittleEndian)

    return None


def _transfer_syntax_uid(ds: pydicom.Dataset) -> str | None:
    file_meta = getattr(ds, "file_meta", None)
    existing = getattr(file_meta, "TransferSyntaxUID", None) if file_meta is not None else None
    if existing:
        return str(existing)
    return _infer_transfer_syntax_uid(ds)


def _ensure_transfer_syntax_uid(ds: pydicom.Dataset) -> str | None:
    """Populate ``file_meta.TransferSyntaxUID`` when pydicom can infer it safely."""
    file_meta = getattr(ds, "file_meta", None)
    existing = getattr(file_meta, "TransferSyntaxUID", None) if file_meta is not None else None
    if existing:
        return None

    tsuid = _infer_transfer_syntax_uid(ds)
    if not tsuid:
        return None

    file_meta = getattr(ds, "file_meta", None)
    if file_meta is None:
        file_meta = FileMetaDataset()
        ds.file_meta = file_meta

    if not getattr(file_meta, "TransferSyntaxUID", None):
        file_meta.TransferSyntaxUID = UID(tsuid)

    return tsuid


def read_dicom_tags(path: str | Path) -> dict[str, str | int | float | None]:
    """Return clinical DICOM tags used for ontology mapping and ``case_media.dicom_metadata``."""
    p = Path(path)
    if not p.is_file():
        raise FileNotFoundError(str(p))

    ds = pydicom.dcmread(str(p), stop_before_pixels=True, force=True)
    _ensure_transfer_syntax_uid(ds)
    lat = ds.get("ImageLaterality") or ds.get("Laterality")

    return {
        "patient_id": _tag_str(ds, "PatientID"),
        "patient_sex": _tag_str(ds, "PatientSex"),
        "patient_age": _tag_str(ds, "PatientAge"),
        "modality": _tag_str(ds, "Modality"),
        "body_part_examined": _tag_str(ds, "BodyPartExamined"),
        "study_description": _tag_str(ds, "StudyDescription"),
        "series_description": _tag_str(ds, "SeriesDescription"),
        "laterality": (str(lat).strip() if lat is not None else None) or None,
        "view_position": _tag_str(ds, "ViewPosition"),
        "slice_thickness": _tag_float(ds, "SliceThickness"),
        "rows": _tag_int(ds, "Rows"),
        "columns": _tag_int(ds, "Columns"),
        "study_instance_uid": _tag_str(ds, "StudyInstanceUID"),
        "series_instance_uid": _tag_str(ds, "SeriesInstanceUID"),
        "sop_instance_uid": _tag_str(ds, "SOPInstanceUID"),
        "instance_number": _tag_int(ds, "InstanceNumber"),
        "photometric_interpretation": _tag_str(ds, "PhotometricInterpretation"),
        "transfer_syntax_uid": _transfer_syntax_uid(ds),
        "source_file_name": p.name,
    }


def build_case_dicom_metadata(
    tags: dict[str, Any],
    *,
    preview_url: str,
    storage_path: str,
    anatomy_site: str,
    laterality: str,
    view_position: str,
    quality_score: float,
    archive_path: str | None = None,
) -> dict[str, Any]:
    """JSON payload persisted to ``case_media.dicom_metadata`` (jsonb)."""
    payload: dict[str, Any] = {
        "ingest": "bonevisqa-ai-ingest",
        "preview_url": preview_url,
        "storage_path": storage_path,
        "preview_format": "png",
        "anatomy_site": anatomy_site,
        "laterality": laterality,
        "view_position": view_position,
        "quality_score": quality_score,
        "patient_id": tags.get("patient_id"),
        "patient_sex": tags.get("patient_sex"),
        "patient_age": tags.get("patient_age"),
        "modality": tags.get("modality"),
        "body_part_examined": tags.get("body_part_examined"),
        "study_description": tags.get("study_description"),
        "series_description": tags.get("series_description"),
        "slice_thickness": tags.get("slice_thickness"),
        "rows": tags.get("rows"),
        "columns": tags.get("columns"),
        "study_instance_uid": tags.get("study_instance_uid"),
        "series_instance_uid": tags.get("series_instance_uid"),
        "sop_instance_uid": tags.get("sop_instance_uid"),
        "instance_number": tags.get("instance_number"),
        "photometric_interpretation": tags.get("photometric_interpretation"),
        "transfer_syntax_uid": tags.get("transfer_syntax_uid"),
        "source_file_name": tags.get("source_file_name"),
    }
    if archive_path:
        payload["source_archive"] = archive_path
    return {k: v for k, v in payload.items() if v is not None}


def extract_dicom_image(path: str | Path) -> Image.Image:
    """
    Decode DICOM pixel data and return RGB PIL image suitable for vision encoders.

    Handles VOI LUT/windowing, MONOCHROME inversion, and 2D/3D frames.
    """
    p = Path(path)
    if not p.is_file():
        raise FileNotFoundError(str(p))

    ds = pydicom.dcmread(str(p), stop_before_pixels=False, force=True)
    inferred_transfer_syntax = _ensure_transfer_syntax_uid(ds)

    try:
        pixels = ds.pixel_array
    except Exception as ex:
        if inferred_transfer_syntax:
            raise RuntimeError(
                "Unable to decode DICOM pixel data after inferring "
                f"Transfer Syntax UID {inferred_transfer_syntax}: {ex}"
            ) from ex
        raise RuntimeError(f"Unable to decode DICOM pixel data: {ex}") from ex

    if pixels.ndim == 3:
        if pixels.shape[-1] in (3, 4):
            first = pixels[..., :3]
            return Image.fromarray(first.astype(np.uint8), mode="RGB")
        pixels = pixels[0]

    if pixels.ndim != 2:
        raise RuntimeError(f"Unsupported DICOM pixel shape: {pixels.shape}")

    try:
        pixels = apply_voi_lut(pixels, ds)
    except Exception:
        pass

    slope = float(ds.get("RescaleSlope", 1.0))
    intercept = float(ds.get("RescaleIntercept", 0.0))
    pixels = pixels.astype(np.float32) * slope + intercept

    if str(ds.get("PhotometricInterpretation", "")).upper() == "MONOCHROME1":
        pixels = np.max(pixels) - pixels

    img_u8 = _normalize_to_uint8(pixels)
    return Image.fromarray(img_u8, mode="L").convert("RGB")
