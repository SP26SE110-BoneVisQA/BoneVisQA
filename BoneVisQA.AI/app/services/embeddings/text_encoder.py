"""Text embedding encoder using sentence-transformers (lazy-loaded singleton)."""

from __future__ import annotations

import os
from functools import lru_cache

import numpy as np

_TEXT_MODEL_NAME = os.environ.get("TEXT_EMBEDDING_MODEL", "sentence-transformers/all-mpnet-base-v2")


@lru_cache(maxsize=1)
def _load_model():
    from sentence_transformers import SentenceTransformer

    return SentenceTransformer(_TEXT_MODEL_NAME)


def encode_text(text: str) -> np.ndarray:
    """Return normalized float32 sentence embedding."""
    return encode_texts([text])[0]


def encode_texts(texts: list[str]) -> list[np.ndarray]:
    """Batch-encode texts (single model load / forward pass)."""
    if not texts:
        return []

    normalized = [(t or "").strip() or "no diagnosis" for t in texts]
    vecs = _load_model().encode(
        normalized,
        normalize_embeddings=True,
        convert_to_numpy=True,
        show_progress_bar=False,
        batch_size=min(32, len(normalized)),
    )
    return [v.astype(np.float32) for v in vecs]


def text_model_name() -> str:
    return _TEXT_MODEL_NAME


def warmup_text_model() -> None:
    """Load weights at process start so the first enrich request does not cold-start."""
    _load_model()
