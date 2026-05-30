"""Strict medical ontology literals aligned with case_metadata + C# promotion DTOs."""

from __future__ import annotations

import math
from typing import Any

MODALITIES_DB = frozenset({"X-Ray", "X-ray", "CT", "MRI", "Ultrasound"})
ANATOMY_SITES = frozenset(
    {"Spine", "Hip", "Knee", "Wrist", "Shoulder", "Ankle", "Pelvis", "Foot", "Hand", "Elbow", "Femur", "Tibia", "Fibula", "Other"}
)
LATERALITIES = frozenset({"Left", "Right", "Bilateral", "Not-Applicable"})
VIEW_POSITIONS = frozenset({"AP", "Lateral", "Oblique", "PA"})
PATHOLOGY_GROUPS = frozenset({"Trauma", "Degenerative", "Infection", "Tumor", "Congenital"})
DIFFICULTIES = frozenset({"Easy", "Medium", "Hard"})
SOURCE_TYPES = frozenset({"Clinical", "Training", "Research"})


def clamp_quality(score: float | None, *, default: float = 0.75) -> float:
    if score is None or math.isnan(score):
        return default
    return float(max(0.0, min(1.0, score)))


def validate_enum(label: str, value: str | None, allowed: frozenset[str]) -> str:
    if not value or not str(value).strip():
        raise ValueError(f"{label} is required.")
    v = str(value).strip()
    if v not in allowed:
        raise ValueError(f"{label} must be one of {sorted(allowed)}; got {v!r}.")
    return v


def clinical_context_payload(
    *,
    source: str,
    differential_diagnoses: list[str] | None = None,
    clinical_evidence: str | None = None,
    extra: dict[str, Any] | None = None,
) -> dict[str, Any]:
    payload: dict[str, Any] = {"source": source}
    if differential_diagnoses:
        payload["differential_diagnoses"] = [d.strip() for d in differential_diagnoses if d and str(d).strip()]
    if clinical_evidence and clinical_evidence.strip():
        payload["clinical_evidence"] = clinical_evidence.strip()
    if extra:
        payload.update(extra)
    return payload
