"""Tier 2: Anatomy — map BodyPartExamined (+ optional Vietnamese text) to region buckets."""

from __future__ import annotations

TIER2_VALUES = frozenset({"Upper Limb", "Lower Limb", "Spine", "Pelvis/Hip"})

# Uppercase BodyPartExamined tokens and aliases (institutional codes included).
_BODY_MAP: dict[str, str] = {
    "PELVIS": "Pelvis/Hip",
    "HIP": "Pelvis/Hip",
    "LOW_EXM": "Lower Limb",
    "LOWER_EXTREMITY": "Lower Limb",
    "LOWER EXTREMITY": "Lower Limb",
    "FOOT": "Lower Limb",
    "ANKLE": "Lower Limb",
    "KNEE": "Lower Limb",
    "FEMUR": "Lower Limb",
    "TIBIA": "Lower Limb",
    "FIBULA": "Lower Limb",
    "HAND": "Upper Limb",
    "WRIST": "Upper Limb",
    "ELBOW": "Upper Limb",
    "SHOULDER": "Upper Limb",
    "HUMERUS": "Upper Limb",
    "RADIUS": "Upper Limb",
    "ULNA": "Upper Limb",
    "CLAVICLE": "Upper Limb",
    "CSPINE": "Spine",
    "TSPINE": "Spine",
    "LSPINE": "Spine",
    "SPINE": "Spine",
    "LUMBAR": "Spine",
    "CERVICAL": "Spine",
}


def map_anatomy_tier2(body: str | None) -> str:
    """Map DICOM BodyPartExamined (or fragment) to exactly one tier-2 region."""
    b = (body or "").strip().upper().replace(" ", "_")
    if not b:
        return "Lower Limb"

    if b in _BODY_MAP:
        return _BODY_MAP[b]

    for key, val in _BODY_MAP.items():
        if key in b or b in key:
            return val

    return "Lower Limb"


def infer_anatomy_from_text(vi: str | None) -> str | None:
    """
    Optional fallback when BodyPartExamined is missing or non-standard (e.g. LOW_EXM).

    Returns None if no keyword matches.
    """
    if not vi:
        return None
    t = vi.lower()
    if any(
        k in t
        for k in (
            "cột sống",
            "thắt lưng",
            "cổ ",
            "đĩa đệm",
            "cstl",
            "đốt sống",
        )
    ):
        return "Spine"
    if any(k in t for k in ("khớp háng", "xương chậu", "vùng chậu", "hông ", " hông")):
        return "Pelvis/Hip"
    if any(
        k in t
        for k in (
            "cẳng chân",
            "gối",
            "mắt cá",
            "bàn chân",
            "chày",
            "mác",
            "đùi",
            "cổ chân",
            "xương chày",
            "mâm chày",
        )
    ):
        return "Lower Limb"
    if any(
        k in t
        for k in (
            "cẳng tay",
            "cổ tay",
            "khuỷu",
            "vai",
            "cánh tay",
            "xương quay",
            "xương trụ",
            "xương đòn",
            "ngón",
            "bàn tay",
            "chỏm xương",
        )
    ):
        return "Upper Limb"
    return None
