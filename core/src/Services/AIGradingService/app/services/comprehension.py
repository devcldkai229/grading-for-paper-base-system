"""Comprehension (doc 5.3 — step B3).

Reads the student's answer for one question and extracts the claims they make, each with a verbatim
supporting quote. NO scoring happens here — this is a neutral understanding pass whose output feeds
the verdict grader (B4). Keeping comprehension separate from judgement reduces the chance the model
"reads in" credit that isn't there.
"""

from __future__ import annotations

import json
import logging
from typing import Any

from app.config import settings
from app.infra.llm_semaphore import llm_slot
from app.infra.openai_client import get_openai

logger = logging.getLogger(__name__)

_SYSTEM = (
    "Bạn là trợ lý đọc hiểu bài làm học sinh. Nhiệm vụ: liệt kê các Ý/khẳng định mà học sinh nêu, "
    "kèm trích dẫn NGUYÊN VĂN từ bài làm cho mỗi ý. TUYỆT ĐỐI KHÔNG chấm điểm, không đánh giá đúng sai, "
    "không suy diễn thêm điều học sinh không viết. Chỉ xuất JSON hợp lệ."
)

_SCHEMA: dict[str, Any] = {
    "name": "student_claims",
    "strict": True,
    "schema": {
        "type": "object",
        "properties": {
            "claims": {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "claim": {"type": "string"},
                        "quote": {"type": "string"},
                    },
                    "required": ["claim", "quote"],
                    "additionalProperties": False,
                },
            }
        },
        "required": ["claims"],
        "additionalProperties": False,
    },
}


async def extract_claims(
    question_text: str, answer_text: str, model: str | None = None
) -> list[dict[str, str]]:
    """Extract student claims + verbatim quotes. Returns [] on empty answer or failure."""
    if not answer_text.strip():
        return []

    model = model or settings.openai_model_t1
    user = (
        f"## Câu hỏi\n{question_text or '(không có)'}\n\n"
        f"## Bài làm học sinh\n{answer_text}\n\n"
        "Liệt kê các ý học sinh nêu kèm trích dẫn nguyên văn."
    )
    try:
        client = get_openai()
        async with llm_slot():
            completion = await client.chat.completions.create(
                model=model,
                messages=[
                    {"role": "system", "content": _SYSTEM},
                    {"role": "user", "content": user},
                ],
                response_format={"type": "json_schema", "json_schema": _SCHEMA},
                temperature=0,
            )
        data = json.loads(completion.choices[0].message.content or "{}")
        return [
            {"claim": str(c.get("claim", "")), "quote": str(c.get("quote", ""))}
            for c in data.get("claims", [])
        ]
    except Exception as exc:  # noqa: BLE001 — comprehension is best-effort context
        logger.warning("Comprehension failed: %s", exc)
        return []
