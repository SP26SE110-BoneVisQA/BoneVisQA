"""Validate Hugging Face model repo ids from environment variables."""

from __future__ import annotations

import logging
import os
import re

logger = logging.getLogger(__name__)

_HF_REPO_RE = re.compile(
    r"^[A-Za-z0-9][A-Za-z0-9._-]*/[A-Za-z0-9][A-Za-z0-9._-]*$"
)

_SECRET_ENV_NAMES = (
    "HUGGINGFACE_API_KEY",
    "HF_TOKEN",
    "HUGGING_FACE_HUB_TOKEN",
    "HUGGINGFACEHUB_API_TOKEN",
)


def _known_hf_secrets() -> frozenset[str]:
    out: set[str] = set()
    for name in _SECRET_ENV_NAMES:
        value = (os.environ.get(name) or "").strip()
        if value:
            out.add(value)
    return frozenset(out)


def is_valid_hf_model_id(value: str) -> bool:
    """True when value looks like ``org/model``, not an API token."""
    candidate = value.removeprefix("hf-hub:").strip()
    if not candidate or len(candidate) > 96:
        return False
    # HF tokens are often base64-like and contain ``+`` or ``=``.
    if "+" in candidate or "=" in candidate:
        return False
    if candidate.lower().startswith("hf_") and "/" not in candidate:
        return False
    return bool(_HF_REPO_RE.match(candidate))


def _mask(value: str, *, limit: int = 24) -> str:
    if len(value) <= limit:
        return value
    return value[:limit] + "…"


def resolve_hf_model_id(env_var: str, default: str) -> str:
    """
    Read ``env_var`` as a Hugging Face repo id (``org/model``).

    Falls back to ``default`` when unset, invalid, or accidentally set to an API token.
    """
    raw = (os.environ.get(env_var) or "").strip()
    secrets = _known_hf_secrets()

    if raw and raw in secrets:
        logger.error(
            "%s is set to your Hugging Face API token. Put the token in HUGGINGFACE_API_KEY only; "
            "set %s to a repo id such as %s or remove it to use the default.",
            env_var,
            env_var,
            default,
        )
        raw = ""

    if raw and not is_valid_hf_model_id(raw):
        logger.error(
            "%s=%r is not a valid Hugging Face repo id (expected org/model). Using default %s.",
            env_var,
            _mask(raw),
            default,
        )
        raw = ""

    if raw:
        return raw.removeprefix("hf-hub:").strip()
    return default
