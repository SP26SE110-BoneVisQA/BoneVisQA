"""Rule-based per-chunk metadata for PDF knowledge-base chunks (no LLM)."""

from __future__ import annotations

import re

from app.services.ontology.anatomy_site import infer_anatomy_site
from app.services.ontology.medical_metadata import ANATOMY_SITES, PATHOLOGY_GROUPS
from app.services.ontology.tier3_pathology import _RULES

_CANONICAL_MODALITIES = frozenset({"X-Ray", "CT", "MRI", "Ultrasound"})

_SECTION_HEADING = re.compile(
    r"^(?:"
    r"(?:chương|chapter|mục|phần|part|bài|section)\s+[\divxlcdm]+"
    r"|[\d]+[\.\)]\s+[A-ZÀÁẢÃẠĂẰẮẲẴẶÂẦẤẨẪẬĐÈÉẺẼẸÊỀẾỂỄỆ"
    r"ÍÌỈĨỊÓÒỎÕỌÔỒỐỔỖỘƠỜỚỞỠỢÚÙỦŨỤƯỪỨỬỮỰÝỲỶỸỴ"
    r"A-Z][^\n]{0,120}"
    r")$",
    re.IGNORECASE | re.MULTILINE,
)

_ALL_CAPS_LINE = re.compile(
    r"^[A-ZÀÁẢÃẠĂẰẮẲẴẶÂẦẤẨẪẬĐÈÉẺẼẸÊỀẾỂỄỆ"
    r"ÍÌỈĨỊÓÒỎÕỌÔỒỐỔỖỘƠỜỚỞỠỢÚÙỦŨỤƯỪỨỬỮỰÝỲỶỸỴ"
    r"0-9\s,\-:/]{4,90}$"
)


def normalize_modality(raw: str | None) -> str:
    if not raw or not str(raw).strip():
        return "X-Ray"
    v = str(raw).strip()
    if v.lower() in {"x-ray", "xray", "x ray", "dx", "cr", "xr"}:
        return "X-Ray"
    if v.upper() == "CT":
        return "CT"
    if v.upper() in {"MR", "MRI"}:
        return "MRI"
    if v.lower() in {"us", "ultrasound"}:
        return "Ultrasound"
    if v in _CANONICAL_MODALITIES:
        return v
    return "X-Ray"


def infer_pathology_from_text(text: str | None) -> str | None:
    s = (text or "").strip()
    if not s:
        return None
    for pat, label in _RULES:
        if pat.search(s):
            return label
    return None


def is_section_heading(line: str) -> bool:
    t = (line or "").strip()
    if len(t) < 4 or len(t) > 160:
        return False
    if _SECTION_HEADING.match(t):
        return True
    if _ALL_CAPS_LINE.match(t) and sum(c.isalpha() for c in t) >= 4:
        return True
    return False


def section_metadata_from_heading(heading: str) -> tuple[str | None, str | None]:
    anatomy = infer_anatomy_site(None, heading)
    pathology = infer_pathology_from_text(heading)
    return (
        anatomy if anatomy in ANATOMY_SITES and anatomy != "Other" else None,
        pathology if pathology in PATHOLOGY_GROUPS else None,
    )


def resolve_chunk_metadata(
    content: str,
    *,
    section_anatomy: str | None,
    section_pathology: str | None,
    default_modality: str | None,
    default_pathology: str | None,
) -> tuple[str, str, str]:
    """Return (modality, anatomy, pathology_group) for one chunk."""
    modality = normalize_modality(default_modality)

    chunk_anatomy = infer_anatomy_site(None, content)
    anatomy = chunk_anatomy
    if anatomy == "Other" and section_anatomy:
        anatomy = section_anatomy
    if anatomy not in ANATOMY_SITES:
        anatomy = "Other"

    chunk_pathology = infer_pathology_from_text(content)
    pathology = chunk_pathology or section_pathology or default_pathology or "Other"
    if pathology not in PATHOLOGY_GROUPS:
        pathology = "Other"

    return modality, anatomy, pathology


def first_heading_in_chunk(content: str) -> str | None:
    for line in (content or "").splitlines():
        t = line.strip()
        if is_section_heading(t):
            return t
    return None
