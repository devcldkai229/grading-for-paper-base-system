"""Deterministic post-verdict checkers (spec D7 / Sprint 2).

Override or refine LLM verdicts when the compiled answerKey encodes machine-verifiable rules.
"""

from __future__ import annotations

from app.schemas.grading import ScoreGridItem
from app.services.checkers.path_graph import apply_path_graph_checker

_CHECKER_PREFIX = "CHECKER:path_graph"


def apply_deterministic_checkers(
    item: ScoreGridItem,
    answer_text: str,
    verdict_by_check: dict[str, str],
) -> dict[str, str]:
    """Return a possibly adjusted verdict map after deterministic rules."""
    if not item.answer_key:
        return verdict_by_check

    key = item.answer_key.strip()
    if key.upper().startswith(_CHECKER_PREFIX.upper()):
        return apply_path_graph_checker(item, answer_text, verdict_by_check)

    return verdict_by_check
