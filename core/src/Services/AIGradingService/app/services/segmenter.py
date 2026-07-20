"""Answer Segmenter — split a student's submission text into per-parent-question answers.

Strategy:
1. Heuristic pass: regex patterns for common question markers (parent numbers only).
2. Reject heuristic if it finds >2× expected markers (false positives from numbered lists).
3. LLM fallback when coverage is poor.
4. Missing parents → empty string (router flags all leaf criteria as missing).
"""

from __future__ import annotations

import json
import logging
import re
from typing import Any

from app.config import settings
from app.infra.llm_semaphore import llm_slot
from app.infra.openai_client import get_openai

logger = logging.getLogger(__name__)

# ---------------------------------------------------------------------------
# Parent question helpers
# ---------------------------------------------------------------------------

def derive_parent_question(question_number: str) -> str:
    """Derive parent question from a leaf criterion id.

    Examples: ``"1.1" → "1"``, ``"2.a" → "2"``, ``"3" → "3"``, ``"2a" → "2"``.
    """
    q = question_number.strip()
    match = re.match(r"^(\d+)", q)
    if not match:
        return q
    return match.group(1)


# ---------------------------------------------------------------------------
# Heuristic regex patterns — anchored at paragraph / line starts only
# ---------------------------------------------------------------------------

# Marker must be at start of text OR after a blank line (reduces mid-paragraph false positives)
_LINE_START = r"(?:^|(?<=\n\n)|(?<=\n))"

_PATTERNS: list[re.Pattern[str]] = [
    re.compile(_LINE_START + r"(?i)\s*request\s*(\d+)\s*[:\-.\)]", re.MULTILINE),
    re.compile(_LINE_START + r"(?i)\s*(?:câu|cau)\s*(\d+)\s*[:\-.\)]?", re.MULTILINE),
    re.compile(_LINE_START + r"(?i)\s*(?:yêu\s*cầu|yeucau)\s*(\d+)\s*[:\-.\)]?", re.MULTILINE),
    re.compile(_LINE_START + r"(?i)\s*(?:question|part)\s*(\d+)\s*[:\-.\)]?", re.MULTILINE),
    # Bare "1)", "1.", "1:" only at line start after blank line / start of text
    re.compile(_LINE_START + r"\s*(\d+)\s*[\).\-:]", re.MULTILINE),
]


def _heuristic_segment(text: str) -> dict[str, str]:
    """Try to split text into {parentQuestion → answer_text} using regex."""
    hits: list[tuple[int, int, str]] = []

    for pattern in _PATTERNS:
        for m in pattern.finditer(text):
            q_num = m.group(1)
            hits.append((m.start(), m.end(), q_num))

    if not hits:
        return {}

    seen: set[str] = set()
    unique_hits: list[tuple[int, int, str]] = []
    for start, end, q_num in sorted(hits, key=lambda x: x[0]):
        if q_num not in seen:
            seen.add(q_num)
            unique_hits.append((start, end, q_num))

    segments: dict[str, str] = {}
    for i, (_, marker_end, q_num) in enumerate(unique_hits):
        if i + 1 < len(unique_hits):
            next_start = unique_hits[i + 1][0]
            answer = text[marker_end:next_start].strip()
        else:
            answer = text[marker_end:].strip()
        segments[q_num] = answer

    return segments


# ---------------------------------------------------------------------------
# LLM fallback segmenter
# ---------------------------------------------------------------------------

_SEGMENT_SYSTEM_PROMPT = (
    "You segment a student's exam answer into per-question blocks. "
    "Given the full answer text and a list of expected PARENT question numbers "
    "(e.g. 1, 2, 3 — not sub-criteria like 1.1), "
    "return a JSON object mapping each parent questionNumber to the student's answer text. "
    "If a question has no answer, map it to an empty string. "
    "Output JSON only, no prose."
)


async def _llm_segment(
    text: str, expected_questions: list[str]
) -> dict[str, str]:
    """Use gpt-4o-mini to segment the text when heuristics fail."""
    client = get_openai()

    user_prompt = (
        f"Expected parent question numbers: {json.dumps(expected_questions)}\n\n"
        f"Student answer:\n{text}"
    )

    async with llm_slot():
        completion = await client.chat.completions.create(
            model=settings.openai_model_t1,
            messages=[
                {"role": "system", "content": _SEGMENT_SYSTEM_PROMPT},
                {"role": "user", "content": user_prompt},
            ],
            response_format={"type": "json_object"},
            temperature=0.0,
        )

    raw = completion.choices[0].message.content or "{}"
    try:
        result = json.loads(raw)
        if isinstance(result, dict):
            return {str(k): str(v) for k, v in result.items()}
    except (json.JSONDecodeError, TypeError):
        logger.warning("LLM segmenter returned invalid JSON: %s", raw[:200])

    return {}


# ---------------------------------------------------------------------------
# Public API
# ---------------------------------------------------------------------------

async def segment_answers(
    text: str,
    expected_questions: list[str],
) -> dict[str, str]:
    """Segment student submission into per-**parent**-question answers.

    Args:
        text: Normalised full-text of the student's submission.
        expected_questions: Deduplicated parent question numbers (e.g. ``["1","2","3"]``).

    Returns:
        Dict mapping parentQuestion → answer_text.
        Missing parents are mapped to empty string.
    """
    segments = _heuristic_segment(text)

    # False-positive guard: too many markers vs expected → discard heuristic
    if expected_questions and len(segments) > 2 * len(expected_questions):
        logger.warning(
            "Heuristic found %d segments for %d expected parents — rejecting (false positives)",
            len(segments),
            len(expected_questions),
        )
        segments = {}

    matched = set(segments.keys()) & set(expected_questions)
    coverage = len(matched) / len(expected_questions) if expected_questions else 1.0

    logger.info(
        "Heuristic segmenter: %d/%d parent questions matched (%.0f%%)",
        len(matched),
        len(expected_questions),
        coverage * 100,
    )

    if coverage < 0.8 and expected_questions:
        logger.info("Low coverage — invoking LLM segmenter fallback")
        llm_segments = await _llm_segment(text, expected_questions)

        for q_num in expected_questions:
            if q_num not in segments or not segments[q_num].strip():
                segments[q_num] = llm_segments.get(q_num, "")

    for q_num in expected_questions:
        if q_num not in segments:
            segments[q_num] = ""
            logger.debug("Parent question %s not found — marked as missing", q_num)

    return segments
