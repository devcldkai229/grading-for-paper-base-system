"""Coverage gate (doc section 5.6).

Guarantees every score-grid leaf produces a result. The grader occasionally omits a leaf; when
that happens we do ONE targeted retry for just the missing leaves, and anything still missing is
routed to MANUAL_ONLY with an explicit warning (never silently dropped). The pipeline additionally
asserts `len(suggestions) == len(score_grid)` after this gate runs.
"""

from __future__ import annotations

import logging
from collections.abc import Awaitable, Callable

from app.schemas.grading import AngleScore, QuestionSuggestion, ScoreGridItem
from app.services.router import EvalTask

logger = logging.getLogger(__name__)

# grade_fn(tasks, model, rubric_text) -> suggestions
GradeFn = Callable[[list[EvalTask], str, str | None], Awaitable[list[QuestionSuggestion]]]

_MANUAL_RETRY_REASON = (
    "AI không trả về đánh giá cho tiêu chí này sau khi thử lại — cần chấm tay."
)
_MANUAL_MISSING_REASON = "Thiếu kết quả cho tiêu chí này — cần chấm tay."


def manual_only_suggestion(
    question_number: str, reason: str, extra_flags: list[str] | None = None
) -> QuestionSuggestion:
    """Build a zero-score MANUAL_ONLY suggestion carrying an explicit reason."""
    flags = ["manual_only", *(extra_flags or [])]
    return QuestionSuggestion(
        questionNumber=question_number,
        angles={
            angle: AngleScore(s=0, note=reason)
            for angle in ("correctness", "completeness", "relevance", "clarity")
        },
        score=0.0,
        rationale=reason,
        evidence=[],
        confidence=0.0,
        flags=list(dict.fromkeys(flags)),
    )


async def grade_with_coverage(
    cache_miss_tasks: list[EvalTask],
    model: str,
    rubric_text: str | None,
    grade_fn: GradeFn,
) -> tuple[list[QuestionSuggestion], list[str]]:
    """Grade cache-miss leaves; targeted-retry any the LLM skipped; MANUAL fallback the rest.

    Returns exactly one suggestion per input task (order follows `cache_miss_tasks`).
    """
    warnings: list[str] = []
    if not cache_miss_tasks:
        return [], warnings

    suggestions = await grade_fn(cache_miss_tasks, model, rubric_text)
    by_num = {s.question_number: s for s in suggestions}

    expected = {t.question_number for t in cache_miss_tasks}
    missing = [q for q in (t.question_number for t in cache_miss_tasks) if q not in by_num]

    if missing:
        logger.warning(
            "Coverage: %d/%d leaves missing after first pass -> targeted retry: %s",
            len(missing), len(expected), missing,
        )
        missing_set = set(missing)
        retry_tasks = [t for t in cache_miss_tasks if t.question_number in missing_set]
        try:
            retry_suggestions = await grade_fn(retry_tasks, model, rubric_text)
            for s in retry_suggestions:
                by_num.setdefault(s.question_number, s)
        except Exception as exc:  # noqa: BLE001 — retry is best-effort; fall through to MANUAL
            warnings.append(f"Coverage retry failed: {exc}")
            logger.warning("Coverage retry raised: %s", exc)

    result: list[QuestionSuggestion] = []
    for task in cache_miss_tasks:
        suggestion = by_num.get(task.question_number)
        if suggestion is None:
            suggestion = manual_only_suggestion(
                task.question_number, _MANUAL_RETRY_REASON, extra_flags=["coverage_miss"]
            )
            warnings.append(
                f"Q{task.question_number}: no AI suggestion after retry -> MANUAL_ONLY"
            )
        result.append(suggestion)

    return result, warnings


def enforce_full_coverage(
    all_suggestions: list[QuestionSuggestion], score_grid: list[ScoreGridItem]
) -> tuple[list[QuestionSuggestion], list[str]]:
    """Return exactly one suggestion per score-grid leaf, in grid order.

    Any leaf without a suggestion gets a MANUAL_ONLY placeholder; extras not in the grid are dropped.
    This makes `len(result) == len(score_grid)` a hard invariant.
    """
    warnings: list[str] = []
    by_num = {s.question_number: s for s in all_suggestions}

    result: list[QuestionSuggestion] = []
    for item in score_grid:
        suggestion = by_num.get(item.question_number)
        if suggestion is None:
            suggestion = manual_only_suggestion(
                item.question_number, _MANUAL_MISSING_REASON, extra_flags=["coverage_miss"]
            )
            warnings.append(f"Q{item.question_number}: missing from results -> MANUAL_ONLY")
        result.append(suggestion)

    return result, warnings
