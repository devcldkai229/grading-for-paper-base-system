"""Scoring (doc 5.5 — step B6).

CODE — not the LLM — computes the final score for a criterion from the adjudicated per-check-item
verdicts. The LLM only decides yes/partial/no/unclear per check-item; the mapping to a number is
deterministic here so the score is auditable and can never drift from the barem.
"""

from __future__ import annotations

from dataclasses import dataclass, field

from app.schemas.grading import ScoreGridItem


@dataclass
class ScoreResult:
    score: float
    tier_label: str
    satisfied: list[str] = field(default_factory=list)
    partial: list[str] = field(default_factory=list)
    missing_required: list[str] = field(default_factory=list)


def compute_score(item: ScoreGridItem, verdict_by_check: dict[str, str]) -> ScoreResult:
    """Map adjudicated verdicts -> score using the compiled partial-credit tiers.

    Tier rule: pick the highest-scoring tier whose check_ids are ALL satisfied ("yes").
    If no tier qualifies but some checks are yes/partial, fall back to summing check-item
    points (full for "yes", half for "partial") — never collapse to a empty "No Credit"
    tier while the student clearly earned partial credit.
    When there are no tiers at all, always use the point sum.
    """
    satisfied = [c.check_id for c in item.check_items if verdict_by_check.get(c.check_id) == "yes"]
    partial = [c.check_id for c in item.check_items if verdict_by_check.get(c.check_id) == "partial"]
    missing_required = [
        c.check_id for c in item.check_items
        if c.required and verdict_by_check.get(c.check_id) != "yes"
    ]

    def _sum_points() -> float:
        points = sum(c.points for c in item.check_items if verdict_by_check.get(c.check_id) == "yes")
        points += 0.5 * sum(
            c.points for c in item.check_items if verdict_by_check.get(c.check_id) == "partial"
        )
        return points

    if item.partial_credit:
        satisfied_set = set(satisfied)
        applicable = [
            t for t in item.partial_credit
            if t.check_ids and set(t.check_ids).issubset(satisfied_set)
        ]
        if applicable:
            best = max(applicable, key=lambda t: t.score)
            score, tier = best.score, best.label
        elif satisfied or partial:
            # Do not pick empty No Credit tier when student earned something.
            score, tier = _sum_points(), "Tổng hợp điểm thành phần"
        else:
            zero_tiers = [t for t in item.partial_credit if not t.check_ids]
            if zero_tiers:
                lowest = min(zero_tiers, key=lambda t: t.score)
            else:
                lowest = min(item.partial_credit, key=lambda t: t.score)
            score, tier = lowest.score, lowest.label
    else:
        score, tier = _sum_points(), "Tổng hợp điểm thành phần"

    score = max(0.0, min(round(score, 2), item.max_score))
    return ScoreResult(
        score=score,
        tier_label=tier,
        satisfied=satisfied,
        partial=partial,
        missing_required=missing_required,
    )
