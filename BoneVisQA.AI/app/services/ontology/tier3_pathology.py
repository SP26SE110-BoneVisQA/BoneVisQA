"""Tier 3: Pathology group — map Vietnamese diagnosis text to coarse pathology buckets."""

from __future__ import annotations

import re

TIER3_VALUES = frozenset(
    {"Trauma", "Degenerative", "Inflammation", "Tumor", "Congenital"}
)

# Order matters: first regex match wins (more specific groups before generic trauma).
_RULES: list[tuple[re.Pattern[str], str]] = [
    (
        re.compile(
            r"bẩm\s*sinh|dị\s*tật|congenital",
            re.IGNORECASE,
        ),
        "Congenital",
    ),
    (
        re.compile(
            r"bướu|\bu\b|sarcoma|carcinoma|ung\s*thư|neoplasm|tumor",
            re.IGNORECASE,
        ),
        "Tumor",
    ),
    (
        re.compile(
            r"viêm|nhiễm\s*trùng|abscess|inflammation",
            re.IGNORECASE,
        ),
        "Inflammation",
    ),
    (
        re.compile(
            r"thoái\s*hóa|loãng\s*xương|thoái\s*hóa\s*khớp|degenerative",
            re.IGNORECASE,
        ),
        "Degenerative",
    ),
    (
        re.compile(
            r"gãy|chấn\s*thương|đứt|bong|trật|fracture|trauma",
            re.IGNORECASE,
        ),
        "Trauma",
    ),
]


def map_pathology_tier3(vi_diagnosis: str | None) -> str:
    """Map Vietnamese chandoan line to exactly one tier-3 pathology group."""
    s = (vi_diagnosis or "").strip()
    if not s:
        return "Trauma"

    for pat, label in _RULES:
        if pat.search(s):
            return label

    return "Trauma"
