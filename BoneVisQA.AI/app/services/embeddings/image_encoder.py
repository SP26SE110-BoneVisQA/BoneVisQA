"""Vision embedding encoder using CLIP (module-level singleton)."""

from __future__ import annotations

import os

import numpy as np
from PIL import Image
from transformers import CLIPModel, CLIPProcessor

_IMAGE_MODEL_NAME = os.environ.get("IMAGE_EMBEDDING_MODEL", "openai/clip-vit-base-patch32")

_IMAGE_PROCESSOR = CLIPProcessor.from_pretrained(_IMAGE_MODEL_NAME)
_IMAGE_MODEL = CLIPModel.from_pretrained(_IMAGE_MODEL_NAME)


def encode_image(image: Image.Image) -> np.ndarray:
    """Encode PIL image to normalized float32 vector."""
    if image.mode != "RGB":
        image = image.convert("RGB")

    inputs = _IMAGE_PROCESSOR(images=image, return_tensors="pt")
    features = _IMAGE_MODEL.get_image_features(**inputs)
    features = features / features.norm(p=2, dim=-1, keepdim=True)

    vec = features.detach().cpu().numpy()[0]
    return vec.astype(np.float32)


def image_model_name() -> str:
    return _IMAGE_MODEL_NAME
