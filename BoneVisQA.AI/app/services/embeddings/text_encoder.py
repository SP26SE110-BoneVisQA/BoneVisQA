"""Text embedding encoder using sentence-transformers (lazy-loaded singleton)."""

from __future__ import annotations

import os
from functools import lru_cache

import numpy as np

# MiniLM: ~4-6x faster on CPU and ~5x less RAM than all-mpnet-base-v2 (768-d vectors are zero-padded).
_TEXT_MODEL_NAME = os.environ.get(
    "TEXT_EMBEDDING_MODEL",
    "sentence-transformers/all-MiniLM-L6-v2",
)
# Balanced encode batch — pair with ENRICH_BATCH_SIZE=12 on Railway (~8 GB RAM).
_ENCODE_BATCH_SIZE = max(1, int(os.environ.get("ENCODE_BATCH_SIZE", "6")))


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
        batch_size=min(_ENCODE_BATCH_SIZE, len(normalized)),
    )
    return [v.astype(np.float32) for v in vecs]


def text_model_name() -> str:
    return _TEXT_MODEL_NAME


def release_encode_memory() -> None:
    """Drop transient tensors after each enrich batch to reduce OOM risk on small VMs."""
    import gc

    gc.collect()
    try:
        import torch

        if torch.cuda.is_available():
            torch.cuda.empty_cache()
    except Exception:
        pass


def warmup_text_model() -> None:
    """Load weights at process start so the first enrich request does not cold-start."""
    threads = int(os.environ.get("TORCH_NUM_THREADS", "0"))
    if threads > 0:
        try:
            import torch

            torch.set_num_threads(threads)
        except Exception:
            pass
    _load_model()
