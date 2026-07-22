"""Renderer (doc 4.1) — turn a rubric/answer file into page images + text + embedded assets.

Pipeline:
  docx -> (Gotenberg) -> pdf -> (pymupdf) -> 300 DPI page images + text + embedded images
  pdf  -> (pymupdf)   -> page images + text + embedded images
  image (png/jpg)     -> single page image

Results are cached by `sha256(file_bytes)` so re-ingesting the same rubric (or re-grading the same
paper across many leaves) rasterises only once.
"""

from __future__ import annotations

import hashlib
import io
import logging
from collections import OrderedDict
from dataclasses import dataclass, field

import httpx

from app.config import settings

logger = logging.getLogger(__name__)

try:  # pymupdf exposes both names depending on version
    import pymupdf as fitz  # type: ignore
except ImportError:  # pragma: no cover
    import fitz  # type: ignore


@dataclass
class EmbeddedImage:
    """An illustration/figure embedded in the source document."""
    page_index: int
    image_index: int
    data: bytes
    ext: str  # "png" | "jpeg" | ...


@dataclass
class RenderedPage:
    """One rasterised page + its extracted text."""
    page_index: int
    width: int
    height: int
    image_png: bytes
    text: str = ""


@dataclass
class RenderedDocument:
    """Full render result for one source file."""
    sha256: str
    pages: list[RenderedPage] = field(default_factory=list)
    embedded_images: list[EmbeddedImage] = field(default_factory=list)

    @property
    def full_text(self) -> str:
        return "\n\n".join(p.text for p in self.pages if p.text.strip())


# Small in-process LRU cache (rendered images are large; bound the count).
_CACHE_MAX = 16
_cache: "OrderedDict[str, RenderedDocument]" = OrderedDict()


def _cache_get(key: str) -> RenderedDocument | None:
    doc = _cache.get(key)
    if doc is not None:
        _cache.move_to_end(key)
    return doc


def _cache_put(key: str, doc: RenderedDocument) -> None:
    _cache[key] = doc
    _cache.move_to_end(key)
    while len(_cache) > _CACHE_MAX:
        _cache.popitem(last=False)


def _is_docx(content_type: str, url: str = "") -> bool:
    ct = content_type.lower()
    return "word" in ct or "docx" in ct or url.lower().endswith(".docx")


def _is_pdf(content_type: str, url: str = "") -> bool:
    ct = content_type.lower()
    return "pdf" in ct or url.lower().endswith(".pdf")


def _is_image(content_type: str, url: str = "") -> bool:
    ct = content_type.lower()
    return ct.startswith("image/") or url.lower().endswith((".png", ".jpg", ".jpeg", ".webp"))


async def convert_docx_to_pdf(data: bytes, filename: str = "rubric.docx") -> bytes:
    """Convert DOCX -> PDF via Gotenberg (LibreOffice route)."""
    url = f"{settings.gotenberg_url.rstrip('/')}/forms/libreoffice/convert"
    files = {
        "files": (
            filename,
            io.BytesIO(data),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        )
    }
    async with httpx.AsyncClient(timeout=120) as client:
        resp = await client.post(url, files=files)
        resp.raise_for_status()
        return resp.content


def _rasterize_pdf(pdf_bytes: bytes) -> tuple[list[RenderedPage], list[EmbeddedImage]]:
    """Render each PDF page to a PNG at the configured DPI and extract embedded images."""
    zoom = settings.render_dpi / 72.0
    matrix = fitz.Matrix(zoom, zoom)

    pages: list[RenderedPage] = []
    embedded: list[EmbeddedImage] = []

    with fitz.open(stream=pdf_bytes, filetype="pdf") as doc:
        page_limit = min(doc.page_count, settings.max_render_pages)
        for page_index in range(page_limit):
            page = doc[page_index]
            pix = page.get_pixmap(matrix=matrix, alpha=False)
            pages.append(
                RenderedPage(
                    page_index=page_index,
                    width=pix.width,
                    height=pix.height,
                    image_png=pix.tobytes("png"),
                    text=page.get_text("text") or "",
                )
            )
            for image_index, img in enumerate(page.get_images(full=True)):
                xref = img[0]
                try:
                    extracted = doc.extract_image(xref)
                except Exception:  # noqa: BLE001
                    continue
                embedded.append(
                    EmbeddedImage(
                        page_index=page_index,
                        image_index=image_index,
                        data=extracted["image"],
                        ext=extracted.get("ext", "png"),
                    )
                )

    return pages, embedded


def _render_single_image(data: bytes, ext: str) -> RenderedDocument:
    """Wrap a raw image file as a one-page rendered document (normalised to PNG)."""
    sha = hashlib.sha256(data).hexdigest()
    with fitz.open(stream=data, filetype=ext) as doc:
        page = doc[0]
        pix = page.get_pixmap(alpha=False)
        rendered = RenderedPage(
            page_index=0, width=pix.width, height=pix.height, image_png=pix.tobytes("png")
        )
    return RenderedDocument(sha256=sha, pages=[rendered])


async def render_document(
    data: bytes, content_type: str, url: str = ""
) -> RenderedDocument:
    """Render a source file to page images + text + embedded images (cached by sha256)."""
    sha = hashlib.sha256(data).hexdigest()
    cached = _cache_get(sha)
    if cached is not None:
        logger.debug("Renderer cache hit %s", sha[:12])
        return cached

    if _is_docx(content_type, url):
        pdf_bytes = await convert_docx_to_pdf(data)
        pages, embedded = _rasterize_pdf(pdf_bytes)
        doc = RenderedDocument(sha256=sha, pages=pages, embedded_images=embedded)
    elif _is_pdf(content_type, url):
        pages, embedded = _rasterize_pdf(data)
        doc = RenderedDocument(sha256=sha, pages=pages, embedded_images=embedded)
    elif _is_image(content_type, url):
        ext = "png"
        lower = (content_type + url).lower()
        if "jpeg" in lower or "jpg" in lower:
            ext = "jpeg"
        elif "webp" in lower:
            ext = "webp"
        doc = _render_single_image(data, ext)
    else:
        raise ValueError(f"Unsupported content type for rendering: {content_type}")

    logger.info(
        "Rendered %s: %d pages, %d embedded images (%s DPI)",
        sha[:12], len(doc.pages), len(doc.embedded_images), settings.render_dpi,
    )
    _cache_put(sha, doc)
    return doc


def merge_rendered_documents(primary: RenderedDocument, extra: RenderedDocument) -> RenderedDocument:
    """Append pages and embedded images from `extra` onto `primary` (e.g. rubric + exam paper)."""
    offset = len(primary.pages)
    merged_pages = list(primary.pages)
    for page in extra.pages:
        merged_pages.append(
            RenderedPage(
                page_index=offset + page.page_index,
                width=page.width,
                height=page.height,
                image_png=page.image_png,
                text=page.text,
            )
        )

    merged_embedded = list(primary.embedded_images)
    img_offset = len(primary.embedded_images)
    for img in extra.embedded_images:
        merged_embedded.append(
            EmbeddedImage(
                page_index=offset + img.page_index,
                image_index=img_offset + img.image_index,
                data=img.data,
                ext=img.ext,
            )
        )

    return RenderedDocument(
        sha256=primary.sha256,
        pages=merged_pages,
        embedded_images=merged_embedded,
    )
