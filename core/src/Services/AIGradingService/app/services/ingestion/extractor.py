"""Structure extraction (doc 4.2 — step A2).

Runs TWO independent multimodal passes over the rendered barem (page images + text) and
cross-validates them into confidence-tagged blocks. Blocks that both passes agree on are HIGH
confidence; blocks seen by only one pass are MEDIUM/LOW and get surfaced for admin review.

Each block is tagged with a role (question / answer_key / grading_guide / illustration), the
question it belongs to, its page + bbox, and its source (text|vision).
"""

from __future__ import annotations

import json
import logging
from difflib import SequenceMatcher
from typing import Any

from app.config import settings
from app.schemas.contract import BBox, Block, BlockRole, BlockSource, Confidence
from app.services.normalize.renderer import RenderedDocument
from app.services.normalize.vision import vision_json

logger = logging.getLogger(__name__)

_EXTRACT_SYSTEM = """\
Bạn là chuyên gia phân tích cấu trúc đề thi/barem (đáp án). Nhiệm vụ: đọc các trang ảnh và văn \
bản của tài liệu barem, chia thành các KHỐI (block) có nhãn.

Mỗi block có:
- role: "question" (đề bài của một câu), "answer_key" (đáp án/lời giải), \
  "grading_guide" (hướng dẫn chấm/thang điểm), "illustration" (hình/bảng/công thức minh hoạ), \
  hoặc "other".
- question: số câu mà block thuộc về (ví dụ "1", "1.1", "2a"); để "" nếu không rõ.
- text: trích văn bản NGUYÊN VĂN của block (với illustration để mô tả ngắn).
- pageIndex: chỉ số trang (bắt đầu từ 0).
- bbox: vùng bao của block, toạ độ chuẩn hoá 0..1 theo chiều rộng/cao trang \
  (x0,y0 góc trên-trái; x1,y1 góc dưới-phải). Ước lượng hợp lý nếu không chắc.

Chỉ xuất JSON đúng schema. Không thêm văn bản ngoài JSON."""

_EXTRACT_SCHEMA: dict[str, Any] = {
    "name": "rubric_blocks",
    "strict": True,
    "schema": {
        "type": "object",
        "properties": {
            "blocks": {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "role": {
                            "type": "string",
                            "enum": ["question", "answer_key", "grading_guide", "illustration", "other"],
                        },
                        "question": {"type": "string"},
                        "text": {"type": "string"},
                        "pageIndex": {"type": "integer"},
                        "bbox": {
                            "type": "object",
                            "properties": {
                                "x0": {"type": "number"},
                                "y0": {"type": "number"},
                                "x1": {"type": "number"},
                                "y1": {"type": "number"},
                            },
                            "required": ["x0", "y0", "x1", "y1"],
                            "additionalProperties": False,
                        },
                    },
                    "required": ["role", "question", "text", "pageIndex", "bbox"],
                    "additionalProperties": False,
                },
            }
        },
        "required": ["blocks"],
        "additionalProperties": False,
    },
}


def _grid_hint(score_grid: list[dict]) -> str:
    if not score_grid:
        return "(không có lưới điểm)"
    lines = []
    for item in score_grid:
        qn = item.get("questionNumber") or item.get("question_number") or ""
        label = item.get("label") or ""
        mx = item.get("maxScore") or item.get("max_score") or ""
        lines.append(f"- Câu {qn}: {label} (tối đa {mx})")
    return "\n".join(lines)


async def _extract_pass(
    images: list[bytes], text: str, grid_hint: str, temperature: float
) -> list[dict]:
    user_text = (
        "Lưới điểm đã biết (dùng để căn chỉnh số câu):\n"
        f"{grid_hint}\n\n"
        "Văn bản trích từ tài liệu (tham khảo cùng ảnh trang):\n"
        f"{text[:12000]}\n\n"
        "Hãy phân tích các trang ảnh kèm theo và trả về danh sách block."
    )
    result = await vision_json(
        system=_EXTRACT_SYSTEM,
        user_text=user_text,
        images=images,
        schema=_EXTRACT_SCHEMA,
        temperature=temperature,
    )
    return result.get("blocks", []) or []


def _similar(a: str, b: str) -> float:
    return SequenceMatcher(None, a.strip().lower(), b.strip().lower()).ratio()


def _key(raw: dict) -> tuple[str, str]:
    return (str(raw.get("question", "")).strip(), str(raw.get("role", "other")))


def _to_block(raw: dict, index: int, source: BlockSource, confidence: Confidence) -> Block:
    bbox_raw = raw.get("bbox") or {}
    return Block(
        blockId=f"b{index}",
        role=BlockRole(raw.get("role", "other")),
        question=(str(raw.get("question", "")).strip() or None),
        text=str(raw.get("text", "")),
        pageIndex=int(raw.get("pageIndex", 0) or 0),
        bbox=BBox(
            x0=float(bbox_raw.get("x0", 0.0)),
            y0=float(bbox_raw.get("y0", 0.0)),
            x1=float(bbox_raw.get("x1", 1.0)),
            y1=float(bbox_raw.get("y1", 1.0)),
        ),
        source=source,
        confidence=confidence,
    )


def _cross_validate(pass_a: list[dict], pass_b: list[dict]) -> list[Block]:
    """Merge two extraction passes into confidence-tagged blocks."""
    blocks: list[Block] = []
    used_b: set[int] = set()
    idx = 0

    for raw_a in pass_a:
        best_j, best_score = -1, 0.0
        for j, raw_b in enumerate(pass_b):
            if j in used_b or _key(raw_a) != _key(raw_b):
                continue
            score = _similar(raw_a.get("text", ""), raw_b.get("text", ""))
            if score > best_score:
                best_j, best_score = j, score

        if best_j >= 0 and best_score >= 0.6:
            used_b.add(best_j)
            raw_b = pass_b[best_j]
            # keep the longer text; both passes agree -> HIGH
            merged = raw_a if len(raw_a.get("text", "")) >= len(raw_b.get("text", "")) else raw_b
            blocks.append(_to_block(merged, idx, BlockSource.BOTH, Confidence.HIGH))
        else:
            blocks.append(_to_block(raw_a, idx, BlockSource.VISION, Confidence.MEDIUM))
        idx += 1

    for j, raw_b in enumerate(pass_b):
        if j not in used_b:
            blocks.append(_to_block(raw_b, idx, BlockSource.VISION, Confidence.LOW))
            idx += 1

    return blocks


def _coverage_check(blocks: list[Block], score_grid: list[dict]) -> list[str]:
    warnings: list[str] = []
    covered = {b.question for b in blocks if b.question}
    for item in score_grid:
        qn = str(item.get("questionNumber") or item.get("question_number") or "").strip()
        if qn and qn not in covered:
            parent = qn.split(".")[0]
            if parent not in covered:
                warnings.append(f"Câu {qn}: không tìm thấy block nào trong barem")
    guides = {b.question for b in blocks if b.role == BlockRole.GRADING_GUIDE and b.question}
    for item in score_grid:
        qn = str(item.get("questionNumber") or item.get("question_number") or "").strip()
        if qn and qn not in guides and qn.split(".")[0] not in guides:
            warnings.append(f"Câu {qn}: thiếu hướng dẫn chấm (grading_guide)")
    return warnings


async def extract_blocks(
    rendered: RenderedDocument, score_grid: list[dict]
) -> tuple[list[Block], list[str]]:
    """Two-pass, cross-validated structure extraction over a rendered barem."""
    images = [p.image_png for p in rendered.pages]
    if not images:
        return [], ["Không có trang nào để phân tích"]

    grid_hint = _grid_hint(score_grid)
    text = rendered.full_text

    logger.info("Extractor: 2-pass over %d pages", len(images))
    pass_a = await _extract_pass(images, text, grid_hint, temperature=0.1)
    pass_b = await _extract_pass(images, text, grid_hint, temperature=0.4)

    blocks = _cross_validate(pass_a, pass_b)
    warnings = _coverage_check(blocks, score_grid)

    high = sum(1 for b in blocks if b.confidence == Confidence.HIGH)
    logger.info(
        "Extractor: %d blocks (%d high-confidence), %d coverage warnings",
        len(blocks), high, len(warnings),
    )
    return blocks, warnings
