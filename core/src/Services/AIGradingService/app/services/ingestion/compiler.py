"""Contract compiler (doc 4.4 — step A4).

Turns confidence-tagged blocks + the score grid into a CompiledContract: for each leaf criterion it
compiles atomic `check_items`, `partial_credit` tiers, `answer_key`, `common_mistakes`,
`not_penalize`, and flags `requires_visual`. The LLM produces the structure; code assembles the
contract, links assets, and derives `specificity` / `extraction_confidence`.
"""

from __future__ import annotations

import json
import logging
from typing import Any

from app.config import settings
from app.infra.llm_semaphore import llm_slot
from app.infra.openai_client import get_openai
from app.schemas.contract import (
    Block,
    BlockRole,
    CheckItem,
    CompiledContract,
    CompiledCriterion,
    Confidence,
    PartialCreditTier,
    RubricAsset,
    Specificity,
)
from app.services.normalize.renderer import RenderedDocument
from app.services.ingestion.assets import extract_assets
from app.services.ingestion.extractor import extract_blocks

logger = logging.getLogger(__name__)

_COMPILE_SYSTEM = """\
Bạn là chuyên gia thiết kế barem chấm điểm. Với MỘT tiêu chí (câu), dựa trên đề bài, đáp án và \
hướng dẫn chấm được cung cấp, hãy biên dịch thành hợp đồng chấm điểm gồm:
- checkItems: các yêu cầu NGUYÊN TỬ, kiểm chứng được (mỗi ý một dòng), kèm điểm (points) và \
  cờ required (bắt buộc để có điểm).
- partialCredit: các mức điểm (đầy đủ / một phần / không đạt...), mỗi mức nêu rõ điều kiện và \
  danh sách checkIds thoả mãn, cùng số điểm tuyệt đối.
- answerKey: tóm tắt đáp án đúng (nếu có).
- commonMistakes: các lỗi thường gặp bị trừ điểm.
- notPenalize: những điều KHÔNG được trừ điểm (ví dụ khác cách diễn đạt).
- requiresVisual: true nếu việc chấm cần nhìn hình/bảng/công thức.
- specificity: "high" nếu hướng dẫn chấm rõ ràng chi tiết, "medium", hoặc "low" nếu mơ hồ.

Tổng điểm các mức/checkItems phải nhất quán với maxScore. Viết tiếng Việt. Chỉ xuất JSON đúng schema."""

_COMPILE_SCHEMA: dict[str, Any] = {
    "name": "compiled_criterion",
    "strict": True,
    "schema": {
        "type": "object",
        "properties": {
            "checkItems": {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "checkId": {"type": "string"},
                        "description": {"type": "string"},
                        "points": {"type": "number"},
                        "required": {"type": "boolean"},
                        "evidenceHint": {"type": "string"},
                    },
                    "required": ["checkId", "description", "points", "required", "evidenceHint"],
                    "additionalProperties": False,
                },
            },
            "partialCredit": {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "label": {"type": "string"},
                        "score": {"type": "number"},
                        "condition": {"type": "string"},
                        "checkIds": {"type": "array", "items": {"type": "string"}},
                    },
                    "required": ["label", "score", "condition", "checkIds"],
                    "additionalProperties": False,
                },
            },
            "answerKey": {"type": "string"},
            "commonMistakes": {"type": "array", "items": {"type": "string"}},
            "notPenalize": {"type": "array", "items": {"type": "string"}},
            "requiresVisual": {"type": "boolean"},
            "specificity": {"type": "string", "enum": ["high", "medium", "low"]},
        },
        "required": [
            "checkItems", "partialCredit", "answerKey", "commonMistakes",
            "notPenalize", "requiresVisual", "specificity",
        ],
        "additionalProperties": False,
    },
}


def _blocks_for(question: str, blocks: list[Block]) -> list[Block]:
    parent = question.split(".")[0]
    return [b for b in blocks if b.question in (question, parent)]


def _block_text(blocks: list[Block], role: BlockRole) -> str:
    return "\n".join(b.text for b in blocks if b.role == role and b.text.strip())


async def _compile_one(item: dict, blocks: list[Block]) -> tuple[dict, Confidence]:
    qn = str(item.get("questionNumber") or item.get("question_number") or "").strip()
    max_score = float(item.get("maxScore") or item.get("max_score") or 0)
    related = _blocks_for(qn, blocks)

    question_text = _block_text(related, BlockRole.QUESTION)
    answer_key = _block_text(related, BlockRole.ANSWER_KEY)
    guide = _block_text(related, BlockRole.GRADING_GUIDE)

    # extraction confidence = min confidence of the source blocks (empty -> low)
    if not related:
        extraction_conf = Confidence.LOW
    elif all(b.confidence == Confidence.HIGH for b in related):
        extraction_conf = Confidence.HIGH
    elif any(b.confidence == Confidence.LOW for b in related):
        extraction_conf = Confidence.LOW
    else:
        extraction_conf = Confidence.MEDIUM

    user_text = (
        f"Câu {qn} (tối đa {max_score} điểm), nhãn: {item.get('label') or ''}\n\n"
        f"## Đề bài\n{question_text or '(không rõ)'}\n\n"
        f"## Đáp án\n{answer_key or '(không có)'}\n\n"
        f"## Hướng dẫn chấm\n{guide or '(không có)'}\n\n"
        f"Hãy biên dịch tiêu chí này thành hợp đồng chấm điểm (maxScore={max_score})."
    )

    client = get_openai()
    async with llm_slot():
        completion = await client.chat.completions.create(
            model=settings.openai_model_t2,
            messages=[
                {"role": "system", "content": _COMPILE_SYSTEM},
                {"role": "user", "content": user_text},
            ],
            response_format={"type": "json_schema", "json_schema": _COMPILE_SCHEMA},
            temperature=0.1,
        )
    compiled = json.loads(completion.choices[0].message.content or "{}")
    compiled["_questionText"] = question_text
    compiled["_answerKeyBlock"] = answer_key
    compiled["_sourceBlockIds"] = [b.block_id for b in related]
    return compiled, extraction_conf


def _assemble_criterion(
    item: dict, compiled: dict, extraction_conf: Confidence, assets: list[RubricAsset]
) -> CompiledCriterion:
    qn = str(item.get("questionNumber") or item.get("question_number") or "").strip()
    parent = item.get("parentQuestion") or item.get("parent_question") or (qn.split(".")[0] if "." in qn else None)

    check_items = [
        CheckItem(
            checkId=str(c.get("checkId", f"c{i}")),
            description=str(c.get("description", "")),
            points=float(c.get("points", 0) or 0),
            required=bool(c.get("required", False)),
            evidenceHint=(str(c.get("evidenceHint")) or None) if c.get("evidenceHint") else None,
        )
        for i, c in enumerate(compiled.get("checkItems", []))
    ]
    partial = [
        PartialCreditTier(
            label=str(t.get("label", "")),
            score=float(t.get("score", 0) or 0),
            condition=str(t.get("condition", "")),
            checkIds=[str(x) for x in (t.get("checkIds") or [])],
        )
        for t in compiled.get("partialCredit", [])
    ]

    requires_visual = bool(compiled.get("requiresVisual", False))
    visual_asset_ids = (
        [a.asset_id for a in assets if a.question in (qn, parent)] if requires_visual else []
    )

    return CompiledCriterion(
        questionNumber=qn,
        parentQuestion=parent,
        groupLabel=item.get("groupLabel") or item.get("group_label"),
        label=item.get("label"),
        maxScore=float(item.get("maxScore") or item.get("max_score") or 0),
        questionText=compiled.get("_questionText", ""),
        answerKey=(compiled.get("answerKey") or compiled.get("_answerKeyBlock") or None),
        checkItems=check_items,
        partialCredit=partial,
        commonMistakes=[str(x) for x in compiled.get("commonMistakes", [])],
        notPenalize=[str(x) for x in compiled.get("notPenalize", [])],
        requiresVisual=requires_visual,
        visualAssetIds=visual_asset_ids,
        sourceBlockIds=compiled.get("_sourceBlockIds", []),
        specificity=Specificity(compiled.get("specificity", "medium")),
        extractionConfidence=extraction_conf,
    )


async def compile_contract(
    subject_id: str,
    rubric_version: str,
    score_grid: list[dict],
    rendered: RenderedDocument,
) -> CompiledContract:
    """Full ingestion: extract blocks (A2) -> assets (A3) -> compile per-leaf contract (A4)."""
    blocks, warnings = await extract_blocks(rendered, score_grid)
    assets = extract_assets(rendered, blocks)

    criteria: list[CompiledCriterion] = []
    for item in score_grid:
        try:
            compiled, extraction_conf = await _compile_one(item, blocks)
            criteria.append(_assemble_criterion(item, compiled, extraction_conf, assets))
        except Exception as exc:  # noqa: BLE001
            qn = item.get("questionNumber") or item.get("question_number")
            warnings.append(f"Câu {qn}: biên dịch thất bại ({exc}) — cần rà soát thủ công")
            logger.warning("Compile failed for %s: %s", qn, exc)

    coverage_ok = not any("không tìm thấy block" in w for w in warnings) and len(criteria) == len(score_grid)

    contract = CompiledContract(
        subjectId=subject_id,
        rubricVersion=rubric_version,
        language="vi",
        criteria=criteria,
        assets=assets,
        blocks=blocks,
        warnings=warnings,
        coverageOk=coverage_ok,
    )
    logger.info(
        "Compiled contract subject=%s v%s: %d criteria, %d assets, coverage_ok=%s",
        subject_id, rubric_version, len(criteria), len(assets), coverage_ok,
    )
    return contract
