"""DOCX normalizer — extract text via python-docx."""

from __future__ import annotations

import io
import logging

from docx import Document

logger = logging.getLogger(__name__)


def normalize_docx(data: bytes) -> str:
    """Extract plain text from a DOCX file."""
    doc = Document(io.BytesIO(data))
    parts: list[str] = []
    for para in doc.paragraphs:
        text = para.text.strip()
        if text:
            parts.append(text)
    return "\n".join(parts)
