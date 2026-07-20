"""PDF normalizer — extract text via pypdf."""

from __future__ import annotations

import io
import logging

from pypdf import PdfReader

logger = logging.getLogger(__name__)


def normalize_pdf(data: bytes) -> str:
    """Extract plain text from a PDF file."""
    reader = PdfReader(io.BytesIO(data))
    parts: list[str] = []
    for page in reader.pages:
        text = page.extract_text() or ""
        if text.strip():
            parts.append(text.strip())
    return "\n\n".join(parts)
