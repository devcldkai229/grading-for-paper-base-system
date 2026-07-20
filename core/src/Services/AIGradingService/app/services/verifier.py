"""Verifier & Guardrails — post-LLM validation (code is the final gatekeeper).

All checks are deterministic code, NOT AI. Per design doc §7:
- Clamp scores within bounds
- Confidence gate → MANUAL_ONLY
- Evidence check → MANUAL_ONLY
- Evidence grounding (>50% ungrounded) → MANUAL_ONLY
- Sum-check overflow → proportional clamp + MANUAL_ONLY on lowest confidence
- Injection scan → flag (don't penalise student)
"""

from __future__ import annotations

import logging
import re

from app.config import settings
from app.schemas.grading import QuestionSuggestion, ScoreGridItem

logger = logging.getLogger(__name__)

_INJECTION_PATTERNS: list[re.Pattern[str]] = [
    re.compile(r"ignore\s+(all\s+)?(previous\s+)?instructions?", re.IGNORECASE),
    re.compile(r"disregard\s+(the\s+)?rubric", re.IGNORECASE),
    re.compile(r"give\s+(me\s+)?full\s+marks?", re.IGNORECASE),
    re.compile(r"cho\s+(tôi\s+)?điểm\s+tối\s+đa", re.IGNORECASE),
    re.compile(r"cho\s+điểm\s+cao\s+nhất", re.IGNORECASE),
    re.compile(r"you\s+are\s+(now\s+)?a", re.IGNORECASE),
    re.compile(r"bỏ\s+qua\s+(mọi\s+)?chỉ\s+thị", re.IGNORECASE),
    re.compile(r"system\s*:\s*", re.IGNORECASE),
    re.compile(r"<\|im_start\|>", re.IGNORECASE),
]


def _scan_injection(text: str) -> bool:
    """Check if text contains potential prompt injection patterns."""
    for pattern in _INJECTION_PATTERNS:
        if pattern.search(text):
            return True
    return False


def _evidence_grounded(ev: str, answer_lower: str) -> bool:
    """Soft match: quote appears in answer, or ≥50% word overlap."""
    ev_clean = ev.strip().strip("'\"").lower()
    if len(ev_clean) <= 10:
        return True  # too short to judge
    if ev_clean in answer_lower:
        return True
    ev_words = set(ev_clean.split())
    answer_words = set(answer_lower.split())
    if not ev_words:
        return True
    overlap = len(ev_words & answer_words) / len(ev_words)
    return overlap >= 0.5


def verify_suggestions(
    suggestions: list[QuestionSuggestion],
    score_grid: list[ScoreGridItem],
    student_answers: dict[str, str] | None = None,
) -> tuple[list[QuestionSuggestion], list[str]]:
    """Apply guardrails to LLM suggestions.

    Args:
        suggestions: Raw suggestions from the grader.
        score_grid: Rubric score grid for max score bounds.
        student_answers: Optional map keyed by **leaf** questionNumber → answer text
            (typically the parent segment duplicated for each leaf).

    Returns:
        Tuple of (verified_suggestions, warnings).
    """
    warnings: list[str] = []
    grid_map = {item.question_number: item for item in score_grid}
    total_max = sum(item.max_score for item in score_grid)

    for suggestion in suggestions:
        q_num = suggestion.question_number
        grid_item = grid_map.get(q_num)

        if grid_item is None:
            warnings.append(f"Q{q_num}: suggestion for unknown question — skipped")
            suggestion.flags.append("unknown_question")
            suggestion.confidence = 0.0
            continue

        max_score = grid_item.max_score

        # 1. Clamp: 0 ≤ score ≤ maxScore
        original_score = suggestion.score
        suggestion.score = max(0.0, min(suggestion.score, max_score))
        if suggestion.score != original_score:
            warnings.append(
                f"Q{q_num}: score clamped {original_score:.2f} → {suggestion.score:.2f} "
                f"(max={max_score})"
            )

        # 2. Clamp confidence to [0, 1]
        suggestion.confidence = max(0.0, min(1.0, suggestion.confidence))

        # 3. Confidence gate
        if suggestion.confidence < settings.confidence_threshold:
            if "manual_only" not in suggestion.flags:
                suggestion.flags.append("manual_only")
            warnings.append(
                f"Q{q_num}: low confidence {suggestion.confidence:.2f} "
                f"< {settings.confidence_threshold} → MANUAL_ONLY"
            )

        # 4. Empty evidence → MANUAL_ONLY
        if not suggestion.evidence:
            if "manual_only" not in suggestion.flags:
                suggestion.flags.append("manual_only")
            warnings.append(f"Q{q_num}: empty evidence → MANUAL_ONLY")

        # 5. Evidence grounding: >50% ungrounded → MANUAL_ONLY
        if student_answers and q_num in student_answers and suggestion.evidence:
            answer_lower = student_answers[q_num].lower()
            ungrounded = 0
            for i, ev in enumerate(suggestion.evidence):
                if not _evidence_grounded(ev, answer_lower):
                    ungrounded += 1
                    warnings.append(f"Q{q_num}: evidence[{i}] not grounded in answer")

            if ungrounded / len(suggestion.evidence) > 0.5:
                if "manual_only" not in suggestion.flags:
                    suggestion.flags.append("manual_only")
                warnings.append(
                    f"Q{q_num}: >50% evidence ungrounded → MANUAL_ONLY"
                )

        # 6. Injection scan
        if student_answers and q_num in student_answers:
            if _scan_injection(student_answers[q_num]):
                if "possible_injection" not in suggestion.flags:
                    suggestion.flags.append("possible_injection")
                if "manual_only" not in suggestion.flags:
                    suggestion.flags.append("manual_only")
                warnings.append(
                    f"Q{q_num}: possible injection detected in student answer → MANUAL_ONLY "
                    "(score NOT penalised)"
                )

    # 7. Sum-check: clamp proportionally + flag lowest-confidence leaf
    total_suggested = sum(s.score for s in suggestions)
    if total_max > 0 and total_suggested > total_max:
        ratio = total_max / total_suggested
        for s in suggestions:
            s.score = round(s.score * ratio, 2)
        lowest = min(suggestions, key=lambda s: s.confidence)
        if "manual_only" not in lowest.flags:
            lowest.flags.append("manual_only")
        warnings.append(
            f"Total suggested score ({total_suggested:.2f}) exceeds max ({total_max:.2f}) "
            f"— clamped proportionally; Q{lowest.question_number} → MANUAL_ONLY"
        )

    return suggestions, warnings
