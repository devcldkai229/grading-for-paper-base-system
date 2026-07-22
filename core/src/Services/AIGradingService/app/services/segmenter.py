"""Answer Segmenter — split a student's submission into per-parent-question answers (doc 5.1).

Anchor-based, zero-loss design:
1. Locate question markers with **tiered** regex (specific labelled patterns first; bare "1)" numbers
   only as a last resort so numbered lists inside an answer don't create false boundaries).
2. Cut answers directly from the ORIGINAL text between adjacent marker anchors (no rewriting, so no
   content is lost or paraphrased).
3. Run **2 samples** (labelled-only vs labelled+bare) and lower confidence where their boundaries
   disagree.
4. LLM fallback fills parents still missing after the heuristic passes.
5. Every expected parent gets one of 3 states: ``answered`` / ``blank`` / ``not_found``.
"""

from __future__ import annotations

import json
import logging
import re
from dataclasses import dataclass
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
# Segmentation result (3-state + confidence + anchors)
# ---------------------------------------------------------------------------

# Marker must be at start of text OR after a (blank) line — reduces mid-paragraph false positives.
_LINE_START = r"(?:^|(?<=\n\n)|(?<=\n))"

# Tier 1 — specific, labelled markers (low false-positive risk).
_LABELLED_PATTERNS: list[re.Pattern[str]] = [
    re.compile(_LINE_START + r"\s*request\s*(\d+)\s*[:\-.\)]", re.MULTILINE | re.IGNORECASE),
    re.compile(_LINE_START + r"\s*(?:câu|cau)\s*(\d+)\s*[:\-.\)]?", re.MULTILINE | re.IGNORECASE),
    re.compile(_LINE_START + r"\s*(?:yêu\s*cầu|yeucau)\s*(\d+)\s*[:\-.\)]?", re.MULTILINE | re.IGNORECASE),
    re.compile(_LINE_START + r"\s*(?:question|part)\s*(\d+)\s*[:\-.\)]?", re.MULTILINE | re.IGNORECASE),
]

# Tier 2 — bare "1)", "1.", "1:" only at line start. Used only if labelled patterns underperform.
_BARE_PATTERN = re.compile(_LINE_START + r"\s*(\d+)\s*[\).\-:]", re.MULTILINE)

# 3 states for every expected parent question.
STATE_ANSWERED = "answered"
STATE_BLANK = "blank"
STATE_NOT_FOUND = "not_found"


@dataclass
class ParentSegment:
    text: str
    state: str
    confidence: float
    start_anchor: str = ""
    end_anchor: str = ""


@dataclass
class Segmentation:
    """Per-parent segmentation with state + confidence; also exposes a plain text map."""
    parents: dict[str, ParentSegment]

    def text(self, parent: str) -> str:
        seg = self.parents.get(parent)
        return seg.text if seg else ""

    def state(self, parent: str) -> str:
        seg = self.parents.get(parent)
        return seg.state if seg else STATE_NOT_FOUND

    def confidence(self, parent: str) -> float:
        seg = self.parents.get(parent)
        return seg.confidence if seg else 0.0

    def as_text_map(self) -> dict[str, str]:
        return {p: s.text for p, s in self.parents.items()}


# ---------------------------------------------------------------------------
# Heuristic cutting
# ---------------------------------------------------------------------------

@dataclass
class _Cut:
    text: str
    start_anchor: str
    end_anchor: str


def _cut_with_patterns(text: str, patterns: list[re.Pattern[str]]) -> dict[str, _Cut]:
    """Locate markers and cut answers between adjacent anchors (first occurrence of each parent)."""
    hits: list[tuple[int, int, str]] = []
    for pattern in patterns:
        for m in pattern.finditer(text):
            hits.append((m.start(), m.end(), m.group(1)))

    if not hits:
        return {}

    # Keep first occurrence of each parent, ordered by position.
    seen: set[str] = set()
    unique: list[tuple[int, int, str]] = []
    for start, end, q_num in sorted(hits, key=lambda x: x[0]):
        if q_num not in seen:
            seen.add(q_num)
            unique.append((start, end, q_num))

    cuts: dict[str, _Cut] = {}
    for i, (marker_start, marker_end, q_num) in enumerate(unique):
        next_start = unique[i + 1][0] if i + 1 < len(unique) else len(text)
        cuts[q_num] = _Cut(
            text=text[marker_end:next_start].strip(),
            start_anchor=text[marker_start:marker_end].strip(),
            end_anchor=text[next_start:next_start + 40].strip() if next_start < len(text) else "",
        )
    return cuts


# ---------------------------------------------------------------------------
# LLM fallback segmenter
# ---------------------------------------------------------------------------

_SEGMENT_SYSTEM_PROMPT = (
    "Bạn tách bài làm của học sinh thành các phần theo từng câu hỏi CHA "
    "(ví dụ 1, 2, 3 — không phải tiêu chí con như 1.1). "
    "Trả về JSON ánh xạ mỗi questionNumber cha sang phần bài làm tương ứng (nguyên văn). "
    "Câu nào không có bài làm thì ánh xạ sang chuỗi rỗng. Chỉ xuất JSON."
)


async def _llm_segment(
    text: str, expected_questions: list[str], question_texts: dict[str, str] | None
) -> dict[str, str]:
    """Use the aux model to segment when heuristics fall short."""
    client = get_openai()

    hints = ""
    if question_texts:
        hints = "\nĐề bài từng câu (tham khảo để nhận diện ranh giới):\n" + json.dumps(
            question_texts, ensure_ascii=False
        )
    user_prompt = (
        f"Danh sách câu hỏi cha kỳ vọng: {json.dumps(expected_questions)}{hints}\n\n"
        f"Bài làm học sinh:\n{text}"
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

def _coverage(cuts: dict[str, _Cut], expected: list[str]) -> float:
    if not expected:
        return 1.0
    matched = sum(1 for q in expected if q in cuts and cuts[q].text)
    return matched / len(expected)


async def segment_answers(
    text: str,
    expected_questions: list[str],
    question_texts: dict[str, str] | None = None,
) -> Segmentation:
    """Segment a student submission into per-parent answers with state + confidence.

    Args:
        text: Normalised full-text of the student's submission.
        expected_questions: Deduplicated parent question numbers (e.g. ``["1","2","3"]``).
        question_texts: Optional {parent → question text} from the compiled contract (helps the
            LLM fallback recognise boundaries).
    """
    # Two heuristic samples: labelled-only (A) vs labelled+bare (B).
    sample_a = _cut_with_patterns(text, _LABELLED_PATTERNS)
    sample_b = _cut_with_patterns(text, [*_LABELLED_PATTERNS, _BARE_PATTERN])

    cov_a = _coverage(sample_a, expected_questions)
    cov_b = _coverage(sample_b, expected_questions)

    # Prefer the specific sample; only widen to bare numbers when it clearly improves coverage.
    primary = sample_a if cov_a >= cov_b else sample_b

    # False-positive guard: too many markers vs expected → distrust the wider sample.
    if expected_questions and len(primary) > 2 * len(expected_questions):
        logger.warning(
            "Segmenter found %d markers for %d expected parents — falling back to labelled only",
            len(primary), len(expected_questions),
        )
        primary = sample_a

    matched = set(primary.keys()) & set(expected_questions)
    coverage = len(matched) / len(expected_questions) if expected_questions else 1.0
    logger.info(
        "Heuristic segmenter: %d/%d parents matched (%.0f%%)",
        len(matched), len(expected_questions), coverage * 100,
    )

    # LLM fallback for parents still missing / empty.
    llm_segments: dict[str, str] = {}
    if coverage < 0.8 and expected_questions:
        logger.info("Low coverage — invoking LLM segmenter fallback")
        llm_segments = await _llm_segment(text, expected_questions, question_texts)

    parents: dict[str, ParentSegment] = {}
    for q_num in expected_questions:
        cut = primary.get(q_num)
        # Boundary agreement between the two heuristic samples -> confidence.
        a_txt = sample_a.get(q_num).text if q_num in sample_a else None
        b_txt = sample_b.get(q_num).text if q_num in sample_b else None
        agree = a_txt is not None and b_txt is not None and a_txt == b_txt

        if cut and cut.text:
            parents[q_num] = ParentSegment(
                text=cut.text,
                state=STATE_ANSWERED,
                confidence=1.0 if agree else 0.75,
                start_anchor=cut.start_anchor,
                end_anchor=cut.end_anchor,
            )
        elif cut is not None:
            # Marker located but no text after it → the student left this question blank.
            parents[q_num] = ParentSegment(
                text="", state=STATE_BLANK, confidence=0.9, start_anchor=cut.start_anchor,
            )
        elif llm_segments.get(q_num, "").strip():
            parents[q_num] = ParentSegment(
                text=llm_segments[q_num].strip(), state=STATE_ANSWERED, confidence=0.6,
            )
        else:
            # No marker in text and LLM found nothing → the question is absent from the submission.
            parents[q_num] = ParentSegment(text="", state=STATE_NOT_FOUND, confidence=0.8)

    return Segmentation(parents=parents)
