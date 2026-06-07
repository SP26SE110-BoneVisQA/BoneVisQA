"""Vision embedding encoder using BiomedCLIP (lazy-loaded OpenCLIP singleton)."""

from __future__ import annotations

from functools import lru_cache

import numpy as np
import torch
from PIL import Image

from app.services.embeddings.model_config import resolve_hf_model_id

_DEFAULT_HF_ID = "microsoft/BiomedCLIP-PubMedBERT_256-vit_base_patch16_224"
_IMAGE_REPO_ID = resolve_hf_model_id("IMAGE_EMBEDDING_MODEL", _DEFAULT_HF_ID)
_IMAGE_MODEL_ID = f"hf-hub:{_IMAGE_REPO_ID}"


@lru_cache(maxsize=1)
def _load_model() -> tuple[torch.nn.Module, object]:
    from open_clip import create_model_from_pretrained

    model, preprocess = create_model_from_pretrained(_IMAGE_MODEL_ID)
    model.eval()
    return model, preprocess


@lru_cache(maxsize=1)
def image_embedding_dim() -> int:
    """Projection dimension for stored vectors (512 for BiomedCLIP ViT-B/16)."""
    model, _preprocess = _load_model()
    probe = Image.new("RGB", (224, 224))
    tensor = _preprocess(probe).unsqueeze(0)
    with torch.no_grad():
        features = model.encode_image(tensor)
    return int(features.shape[-1])


def encode_image(image: Image.Image) -> np.ndarray:
    """Encode PIL image to L2-normalized float32 vector (BiomedCLIP image tower)."""
    model, preprocess = _load_model()
    if image.mode != "RGB":
        image = image.convert("RGB")

    tensor = preprocess(image).unsqueeze(0)
    with torch.no_grad():
        features = model.encode_image(tensor)
    features = features / features.norm(dim=-1, keepdim=True)

    vec = features.detach().cpu().numpy()[0]
    return vec.astype(np.float32)


def image_model_name() -> str:
    return _IMAGE_REPO_ID


def warmup_image_model() -> None:
    """Load BiomedCLIP at process start so ingest does not fail on first upload."""
    _load_model()
