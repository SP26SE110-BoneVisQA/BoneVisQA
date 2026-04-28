"""Tier 1: Modality — map DICOM Modality to exactly one of X-ray, CT, MRI."""

from __future__ import annotations

TIER1_VALUES = frozenset({"X-ray", "CT", "MRI"})


def map_modality_tier1(raw: str | None) -> str:
    """
    Map DICOM Modality (e.g. DX, CR, CT, MR) to ontology tier 1.

    DX/CR/XR/XA and unknown modalities default to X-ray for MSK radiograph workflows.
    """
    m = (raw or "").strip().upper()
    if m in ("CT", "CTA"):
        return "CT"
    if m in ("MR", "MRI"):
        return "MRI"
    # Radiographs and everything else → X-ray
    if m in (
        "DX",
        "CR",
        "XR",
        "XA",
        "RF",
        "MG",
        "US",
        "PT",
        "NM",
        "OT",
        "",
    ):
        return "X-ray"
    return "X-ray"
