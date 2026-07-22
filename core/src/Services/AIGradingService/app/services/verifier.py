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


# Vietnamese-specific characters (diacritics + đ) — a strong signal the text is Vietnamese.
_VI_DIACRITICS = re.compile(
    r"[àáảãạăằắẳẵặâầấẩẫậèéẻẽẹêềếểễệìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵđ]",
    re.IGNORECASE,
)
# Common Vietnamese function words (covers un-accented Vietnamese too).
_VI_WORDS = {
    "và", "là", "của", "không", "điểm", "đúng", "thiếu", "câu", "học", "sinh",
    "va", "la", "cua", "khong", "diem", "dung", "thieu", "cau", "hoc", "sinh",
    "nên", "cần", "vì", "cho", "được", "có", "đã", "trả", "lời", "bài",
}


def is_vietnamese_text(text: str) -> bool:
    """Deterministic heuristic: is `text` (human feedback) written in Vietnamese?

    Short strings are treated as Vietnamese (nothing to translate). Diacritics are a strong
    positive; otherwise we look for common Vietnamese function words.
    """
    clean = (text or "").strip()
    if len(clean) < 15:
        return True
    if _VI_DIACRITICS.search(clean):
        return True
    words = {w.strip(".,;:!?()[]\"'").lower() for w in clean.split()}
    return len(words & _VI_WORDS) >= 2


def _normalize_ws(text: str) -> str:
    """Lower-case + collapse whitespace so verbatim matching ignores formatting noise only."""
    return " ".join(text.lower().split())


def _evidence_verbatim(ev: str, answer_norm: str) -> bool:
    """Evidence must appear VERBATIM in the answer (whitespace-normalized substring, doc 5.7).

    Very short quotes are exempt (too small to judge). This is stricter than the previous soft
    word-overlap match: an ungrounded quote invalidates the verdict → MANUAL.
    """
    ev_clean = _normalize_ws(ev.strip().strip("'\""))
    if len(ev_clean) <= 10:
        return True  # too short to judge
    return ev_clean in answer_norm


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

        # 2b. Signal penalties for the legacy (non-contract) path. Contract leaves already fold
        # vision + specificity into their composite confidence (confidence.py), so we don't
        # double-penalize them here.
        if not grid_item.has_contract:
            spec = (grid_item.extraction_confidence or grid_item.specificity or "").lower()
            if spec == "low":
                suggestion.confidence *= 0.8
                if "low_specificity" not in suggestion.flags:
                    suggestion.flags.append("low_specificity")
            if "needs_vision" in suggestion.flags:
                suggestion.confidence *= settings.vision_confidence_penalty

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

        # 5. Evidence grounding: quotes must be VERBATIM; >50% ungrounded → MANUAL_ONLY
        if student_answers and q_num in student_answers and suggestion.evidence:
            answer_norm = _normalize_ws(student_answers[q_num])
            ungrounded = 0
            for i, ev in enumerate(suggestion.evidence):
                if not _evidence_verbatim(ev, answer_norm):
                    ungrounded += 1
                    warnings.append(f"Q{q_num}: evidence[{i}] not verbatim in answer")

            if ungrounded / len(suggestion.evidence) > 0.5:
                if "manual_only" not in suggestion.flags:
                    suggestion.flags.append("manual_only")
                warnings.append(
                    f"Q{q_num}: >50% evidence not verbatim → MANUAL_ONLY"
                )

        # 5b. Language check: human-facing rationale must be Vietnamese (doc 5.8). Flag only —
        # the pipeline performs a single re-call to translate flagged rationales (evidence stays
        # verbatim). Deterministic detection here; the actual re-call lives in language.py.
        if suggestion.rationale and not is_vietnamese_text(suggestion.rationale):
            if "language_mismatch" not in suggestion.flags:
                suggestion.flags.append("language_mismatch")
            warnings.append(f"Q{q_num}: rationale not in Vietnamese → queued for re-call")

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

    # 7. Sum-check: total > subject max → MANUAL (NO auto-scale, doc 5.7). Auto-scaling would
    # silently distort per-criterion scores; instead surface it for a human. Flag every leaf so the
    # whole paper is reviewed rather than guessing which criterion is wrong.
    total_suggested = sum(s.score for s in suggestions)
    if total_max > 0 and total_suggested > total_max + 1e-6:
        for s in suggestions:
            if "manual_only" not in s.flags:
                s.flags.append("manual_only")
        warnings.append(
            f"Total suggested score ({total_suggested:.2f}) exceeds max ({total_max:.2f}) "
            "→ ALL criteria MANUAL_ONLY (no auto-scaling)"
        )

    return suggestions, warnings
