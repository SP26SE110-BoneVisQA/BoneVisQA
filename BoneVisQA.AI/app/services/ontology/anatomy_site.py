"""Fine-grained anatomy_site (MSK) from DICOM body part + diagnosis keywords."""

from __future__ import annotations

import re

from app.services.ontology.medical_metadata import ANATOMY_SITES

_SITE_KEYWORDS: list[tuple[re.Pattern[str], str]] = [
    (re.compile(r"\b(spine|đốt\s*sống|cột\s*sống|lumbar|cervical|thoracic|lspine|cspine|tspine)\b", re.I), "Spine"),
    (re.compile(r"\b(hip|hông|khớp\s*háng|pelvis|xương\s*chậu)\b", re.I), "Hip"),
    (re.compile(r"\b(knee|gối|patella)\b", re.I), "Knee"),
    (re.compile(r"\b(wrist|cổ\s*tay)\b", re.I), "Wrist"),
    (re.compile(r"\b(shoulder|vai|scapula)\b", re.I), "Shoulder"),
    (re.compile(r"\b(ankle|mắt\s*cá|cổ\s*chân)\b", re.I), "Ankle"),
    (re.compile(r"\b(foot|bàn\s*chân|calcaneus)\b", re.I), "Foot"),
    (re.compile(r"\b(hand|bàn\s*tay|ngón\s*tay)\b", re.I), "Hand"),
    (re.compile(r"\b(elbow|khuỷu)\b", re.I), "Elbow"),
    (re.compile(r"\b(femur|đùi)\b", re.I), "Femur"),
    (re.compile(r"\b(tibia|chày)\b", re.I), "Tibia"),
    (re.compile(r"\b(fibula|mác)\b", re.I), "Fibula"),
]

_BODY_TOKEN_MAP: dict[str, str] = {
    "KNEE": "Knee",
    "ANKLE": "Ankle",
    "FOOT": "Foot",
    "TOES": "Foot",
    "WRIST": "Wrist",
    "HAND": "Hand",
    "FINGER": "Hand",
    "ELBOW": "Elbow",
    "SHOULDER": "Shoulder",
    "CLAVICLE": "Shoulder",
    "HUMERUS": "Shoulder",
    "HIP": "Hip",
    "PELVIS": "Hip",
    "FEMUR": "Femur",
    "TIBIA": "Tibia",
    "FIBULA": "Fibula",
    "SPINE": "Spine",
    "LSPINE": "Spine",
    "CSPINE": "Spine",
    "TSPINE": "Spine",
    "LUMBAR": "Spine",
    "CERVICAL": "Spine",
}


def infer_anatomy_site(body_part_examined: str | None, diagnosis_text: str | None) -> str:
    """Return a value in ANATOMY_SITES."""
    text = f"{diagnosis_text or ''} {body_part_examined or ''}"
    for pat, site in _SITE_KEYWORDS:
        if pat.search(text):
            return site

    b = (body_part_examined or "").strip().upper().replace(" ", "_")
    if b in _BODY_TOKEN_MAP:
        return _BODY_TOKEN_MAP[b]

    for key, site in _BODY_TOKEN_MAP.items():
        if key in b or b in key:
            return site

    return "Other"


def ensure_anatomy_site(value: str | None, body_part: str | None, diagnosis: str | None) -> str:
    inferred = infer_anatomy_site(body_part, diagnosis)
    if not value or not str(value).strip():
        return inferred
    v = str(value).strip()
    if v not in ANATOMY_SITES:
        return inferred
    return v
