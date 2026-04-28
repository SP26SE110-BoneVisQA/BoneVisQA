"""Text embedding encoder using sentence-transformers (module-level singleton)."""

from __future__ import annotations

import os

import numpy as np
from sentence_transformers import SentenceTransformer

_TEXT_MODEL_NAME = os.environ.get("TEXT_EMBEDDING_MODEL", "sentence-transformers/all-mpnet-base-v2")

_TEXT_MODEL = SentenceTransformer(_TEXT_MODEL_NAME)


def encode_text(text: str) -> np.ndarray:
    """Return normalized float32 sentence embedding."""
    normalized = (text or "").strip()
    if not normalized:
        normalized = "no diagnosis"

    vec = _TEXT_MODEL.encode(
        normalized,
        normalize_embeddings=True,
        convert_to_numpy=True,
        show_progress_bar=False,
    )
    return vec.astype(np.float32)


def text_model_name() -> str:
    return _TEXT_MODEL_NAME
