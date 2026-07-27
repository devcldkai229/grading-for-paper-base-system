"""Asset extraction (doc 4.3 — step A3).

Crops illustration/table/formula regions out of the barem so they can be re-shown to the grader
when a criterion `requires_visual`. Two sources:
  1. Blocks tagged `illustration` with a bbox -> crop that region from the rendered page image.
  2. Images embedded directly in the source document (extracted by the renderer).

Crops are returned as base64 PNG; the .NET store (Phase 2c) persists them to S3 and back-fills URLs.
"""

from __future__ import annotations

import base64
import logging

from app.schemas.contract import BBox, Block, BlockRole, RubricAsset
from app.services.normalize.renderer import RenderedDocument

logger = logging.getLogger(__name__)

try:
    import pymupdf as fitz  # type: ignore
except ImportError:  # pragma: no cover
    import fitz  # type: ignore


def _crop_png(page_png: bytes, bbox: BBox) -> bytes | None:
    """Crop a normalised bbox region out of a PNG page image, returning PNG bytes."""
    try:
        with fitz.open(stream=page_png, filetype="png") as doc:
            page = doc[0]
            rect = page.rect
            clip = fitz.Rect(
                bbox.x0 * rect.width,
                bbox.y0 * rect.height,
                bbox.x1 * rect.width,
                bbox.y1 * rect.height,
            )
            if clip.is_empty or clip.width < 4 or clip.height < 4:
                return None
            pix = page.get_pixmap(clip=clip, alpha=False)
            return pix.tobytes("png")
    except Exception as exc:  # noqa: BLE001
        logger.warning("Asset crop failed: %s", exc)
        return None


def extract_assets(rendered: RenderedDocument, blocks: list[Block]) -> list[RubricAsset]:
    """Produce RubricAssets from illustration blocks (cropped) + embedded images."""
    assets: list[RubricAsset] = []
    pages_by_index = {p.page_index: p for p in rendered.pages}
    idx = 0

    for block in blocks:
        if block.role != BlockRole.ILLUSTRATION or block.bbox is None:
            continue
        page = pages_by_index.get(block.page_index)
        if page is None:
            continue
        crop = _crop_png(page.image_png, block.bbox)
        if crop is None:
            continue
        assets.append(
            RubricAsset(
                assetId=f"a{idx}",
                kind="illustration",
                question=block.question,
                pageIndex=block.page_index,
                bbox=block.bbox,
                imageBase64=base64.b64encode(crop).decode("ascii"),
                mime="image/png",
            )
        )
        idx += 1

    for emb in rendered.embedded_images:
        mime = f"image/{'jpeg' if emb.ext in ('jpg', 'jpeg') else emb.ext}"
        assets.append(
            RubricAsset(
                assetId=f"a{idx}",
                kind="illustration",
                question=None,
                pageIndex=emb.page_index,
                bbox=None,
                imageBase64=base64.b64encode(emb.data).decode("ascii"),
                mime=mime,
            )
        )
        idx += 1

    logger.info("Assets: %d cropped/embedded", len(assets))
    return assets
