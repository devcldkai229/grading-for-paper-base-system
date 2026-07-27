"""Evaluator Router — decide how to grade each leaf criterion.

Slice 1 supports two evaluators:
- empty_check: blank/missing parent answers → score 0, skip LLM (all leaves of that parent)
- llm_text:    default for all other criteria → gpt-4o-mini T1

Each EvalTask is one **leaf** criterion, but answer_text comes from the **parent** segment.
"""

from __future__ import annotations

import logging
from dataclasses import dataclass, field
from enum import Enum

from app.schemas.grading import ScoreGridItem
from app.services.segmenter import (
    STATE_NOT_FOUND,
    Segmentation,
    derive_parent_question,
)

logger = logging.getLogger(__name__)


class Evaluator(str, Enum):
    EMPTY_CHECK = "empty_check"
    LLM_TEXT = "llm_text"


@dataclass
class EvalTask:
    """A single evaluation task produced by the router (one leaf criterion)."""
    question_number: str
    evaluator: Evaluator
    answer_text: str
    max_score: float
    parent_question: str
    group_label: str | None = None
    label: str | None = None
    scoring_rule: str | None = None
    answer_key: str | None = None
    flags: list[str] = field(default_factory=list)


def resolve_parent(item: ScoreGridItem) -> str:
    """Parent question for a score-grid leaf."""
    if item.parent_question:
        return item.parent_question.strip()
    return derive_parent_question(item.question_number)


def route_questions(
    segmentation: Segmentation,
    score_grid: list[ScoreGridItem],
) -> list[EvalTask]:
    """Route each leaf criterion to an evaluator using its parent segment.

    Args:
        segmentation: Per-parent Segmentation (text + 3-state + confidence) from the segmenter.
        score_grid: Leaf criteria from the rubric scoring grid.

    Returns:
        List of EvalTask, one per leaf in the score grid.
    """
    tasks: list[EvalTask] = []

    for item in score_grid:
        q_num = item.question_number
        parent = resolve_parent(item)
        answer = segmentation.text(parent)
        is_empty = not answer.strip()

        if is_empty:
            # Distinguish "not_found" (segmenter never located the parent question) from "blank"
            # (parent located but the student left it empty) — doc's 3-state segmentation vocabulary.
            state_flag = "not_found" if segmentation.state(parent) == STATE_NOT_FOUND else "blank"
            task = EvalTask(
                question_number=q_num,
                evaluator=Evaluator.EMPTY_CHECK,
                answer_text="",
                max_score=item.max_score,
                parent_question=parent,
                group_label=item.group_label,
                label=item.label,
                scoring_rule=item.scoring_rule,
                answer_key=item.answer_key,
                flags=[state_flag],
            )
            logger.debug("Leaf %s (parent %s) → empty_check (%s)", q_num, parent, task.flags)
        else:
            task = EvalTask(
                question_number=q_num,
                evaluator=Evaluator.LLM_TEXT,
                answer_text=answer,
                max_score=item.max_score,
                parent_question=parent,
                group_label=item.group_label,
                label=item.label,
                scoring_rule=item.scoring_rule,
                answer_key=item.answer_key,
            )
            logger.debug(
                "Leaf %s (parent %s) → llm_text (answer len=%d)",
                q_num, parent, len(answer),
            )

        tasks.append(task)

    empty_count = sum(1 for t in tasks if t.evaluator == Evaluator.EMPTY_CHECK)
    llm_count = sum(1 for t in tasks if t.evaluator == Evaluator.LLM_TEXT)
    logger.info(
        "Router: %d leaf tasks — %d empty_check, %d llm_text",
        len(tasks), empty_count, llm_count,
    )

    return tasks
