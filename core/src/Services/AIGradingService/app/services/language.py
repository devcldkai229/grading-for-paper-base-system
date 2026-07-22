"""Vietnamese feedback enforcement (doc 5.8).

The verifier flags any suggestion whose `rationale` is not Vietnamese. This module performs the
single, batched re-call that translates the human-facing feedback (rationale + angle notes) into
Vietnamese. `evidence` is NEVER touched — quotes must remain verbatim in the student's language.
"""

from __future__ import annotations

import json
import logging
from typing import Any

from app.config import settings
from app.infra.llm_semaphore import llm_slot
from app.infra.openai_client import get_openai
from app.schemas.grading import QuestionSuggestion

logger = logging.getLogger(__name__)

_TRANSLATE_SYSTEM = (
    "Bạn là biên dịch viên. Hãy dịch các nhận xét chấm bài sang TIẾNG VIỆT tự nhiên, "
    "giữ nguyên ý nghĩa và thuật ngữ chuyên môn. Chỉ dịch phần nhận xét dành cho người đọc. "
    "Chỉ xuất JSON hợp lệ, không thêm văn bản nào khác."
)

_TRANSLATE_SCHEMA: dict[str, Any] = {
    "name": "translated_feedback",
    "strict": True,
    "schema": {
        "type": "object",
        "properties": {
            "items": {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "questionNumber": {"type": "string"},
                        "rationale": {"type": "string"},
                        "notes": {
                            "type": "object",
                            "properties": {
                                "correctness": {"type": "string"},
                                "completeness": {"type": "string"},
                                "relevance": {"type": "string"},
                                "clarity": {"type": "string"},
                            },
                            "required": ["correctness", "completeness", "relevance", "clarity"],
                            "additionalProperties": False,
                        },
                    },
                    "required": ["questionNumber", "rationale", "notes"],
                    "additionalProperties": False,
                },
            }
        },
        "required": ["items"],
        "additionalProperties": False,
    },
}


async def ensure_vietnamese(
    suggestions: list[QuestionSuggestion], model: str | None = None
) -> tuple[list[QuestionSuggestion], list[str]]:
    """Re-call ONCE to translate non-Vietnamese rationales/notes into Vietnamese.

    Only suggestions flagged `language_mismatch` by the verifier are affected. On success the flag
    is cleared. Best-effort: any failure leaves the original text and keeps the flag.
    """
    warnings: list[str] = []
    flagged = [s for s in suggestions if "language_mismatch" in s.flags]
    if not flagged:
        return suggestions, warnings

    model = model or settings.openai_model_t1
    payload = [
        {
            "questionNumber": s.question_number,
            "rationale": s.rationale,
            "notes": {name: angle.note for name, angle in s.angles.items()},
        }
        for s in flagged
    ]
    user_prompt = (
        "Dịch các nhận xét sau sang tiếng Việt (giữ nguyên questionNumber):\n"
        f"```json\n{json.dumps(payload, ensure_ascii=False, indent=2)}\n```"
    )

    try:
        client = get_openai()
        async with llm_slot():
            completion = await client.chat.completions.create(
                model=model,
                messages=[
                    {"role": "system", "content": _TRANSLATE_SYSTEM},
                    {"role": "user", "content": user_prompt},
                ],
                response_format={"type": "json_schema", "json_schema": _TRANSLATE_SCHEMA},
                temperature=0,
            )
        translated = json.loads(completion.choices[0].message.content or "{}")
    except Exception as exc:  # noqa: BLE001 — translation is best-effort
        warnings.append(f"Vietnamese re-call failed: {exc}")
        logger.warning("Vietnamese re-call failed: %s", exc)
        return suggestions, warnings

    by_num = {s.question_number: s for s in flagged}
    for item in translated.get("items", []):
        target = by_num.get(str(item.get("questionNumber", "")))
        if target is None:
            continue
        new_rationale = str(item.get("rationale", "")).strip()
        if new_rationale:
            target.rationale = new_rationale
        notes = item.get("notes", {}) or {}
        for name, angle in target.angles.items():
            translated_note = str(notes.get(name, "")).strip()
            if translated_note:
                angle.note = translated_note
        if "language_mismatch" in target.flags:
            target.flags.remove("language_mismatch")

    logger.info("Vietnamese re-call translated %d suggestions", len(flagged))
    return suggestions, warnings
