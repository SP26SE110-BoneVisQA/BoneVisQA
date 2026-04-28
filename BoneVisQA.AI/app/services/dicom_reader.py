"""Load key tags and pixel data from DICOM via pydicom."""

from __future__ import annotations

import os
import tempfile
from contextlib import contextmanager
from pathlib import Path

import httpx
import numpy as np
import pydicom
from pydicom.pixel_data_handlers.util import apply_voi_lut
from PIL import Image


def is_remote_dicom_reference(dicom_path: str) -> bool:
    """True if ``dicom_path`` is an HTTP(S) URL (case-insensitive scheme)."""
    s = (dicom_path or "").strip().lower()
    return s.startswith("http://") or s.startswith("https://")


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


def read_dicom_tags(path: str | Path) -> dict[str, str | None]:
    """Return PatientID, raw Modality, BodyPartExamined (no ontology mapping)."""
    p = Path(path)
    if not p.is_file():
        raise FileNotFoundError(str(p))

    ds = pydicom.dcmread(str(p), stop_before_pixels=True)
    pid = ds.get("PatientID")
    mod = ds.get("Modality")
    body = ds.get("BodyPartExamined")

    return {
        "patient_id": (str(pid).strip() if pid is not None else None) or None,
        "modality": (str(mod).strip() if mod is not None else None) or None,
        "body_part_examined": (str(body).strip() if body is not None else None) or None,
    }


def extract_dicom_image(path: str | Path) -> Image.Image:
    """
    Decode DICOM pixel data and return RGB PIL image suitable for vision encoders.

    Handles VOI LUT/windowing, MONOCHROME inversion, and 2D/3D frames.
    """
    p = Path(path)
    if not p.is_file():
        raise FileNotFoundError(str(p))

    ds = pydicom.dcmread(str(p), stop_before_pixels=False)

    try:
        pixels = ds.pixel_array
    except Exception as ex:
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
