"""Text file normalizer — Slice 1 supports .txt only.

Reads raw bytes, detects encoding, normalises to clean UTF-8 text.
Future slices will add docx, pdf, pptx, xlsx normalisers alongside this module.
"""

from __future__ import annotations

import hashlib
import logging

from app.infra.redis_cache import get_cached, set_cached

logger = logging.getLogger(__name__)

_NORMALIZE_CACHE_PREFIX = "norm:"


def _detect_and_decode(data: bytes) -> str:
    """Decode bytes to string, trying UTF-8 first, then latin-1 fallback."""
    # Strip UTF-8 BOM if present
    if data.startswith(b"\xef\xbb\xbf"):
        data = data[3:]

    try:
        return data.decode("utf-8")
    except UnicodeDecodeError:
        logger.debug("UTF-8 decode failed, falling back to latin-1")
        return data.decode("latin-1")


def _normalise_line_endings(text: str) -> str:
    """Normalise all line endings to LF."""
    return text.replace("\r\n", "\n").replace("\r", "\n")


def normalize_txt(data: bytes) -> str:
    """Normalise a .txt file to clean UTF-8 text.

    Returns:
        Cleaned text string ready for segmentation.
    """
    text = _detect_and_decode(data)
    text = _normalise_line_endings(text)
    text = text.strip()
    return text


def file_hash(data: bytes) -> str:
    """SHA-256 hex digest of file contents."""
    return hashlib.sha256(data).hexdigest()


async def normalize_txt_cached(data: bytes) -> str:
    """Normalise with cache: same file bytes → same output, no re-processing.

    Particularly important when vision processing is added (expensive).
    For txt this is cheap, but the pattern is established for future formats.
    """
    digest = file_hash(data)
    cache_key = f"{_NORMALIZE_CACHE_PREFIX}{digest}"

    cached = await get_cached(cache_key)
    if cached is not None:
        logger.debug("Normalize cache hit: %s", digest[:12])
        return cached

    text = normalize_txt(data)

    await set_cached(cache_key, text, ttl=604800)  # 7 days
    logger.debug("Normalize cache set: %s (%d chars)", digest[:12], len(text))
    return text
