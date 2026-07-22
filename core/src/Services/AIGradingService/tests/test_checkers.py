"""Tests for deterministic path-graph checker (PMG201c R4)."""

from __future__ import annotations

from app.schemas.grading import ContractCheckItem, ScoreGridItem
from app.services.checkers import apply_deterministic_checkers
from app.services.scoring import compute_score
from app.schemas.grading import ContractPartialCredit


def _r4_item() -> ScoreGridItem:
    return ScoreGridItem(
        questionNumber="4",
        maxScore=2.0,
        answerKey=(
            "CHECKER:path_graph\n"
            "PATHS: ABCG=15; DBCG=16; DEFG=17; DHI=19"
        ),
        checkItems=[
            ContractCheckItem(checkId="c1", description="Liệt kê đúng các path", points=1.0, required=True),
            ContractCheckItem(checkId="c2", description="Tổng path khớp sơ đồ", points=1.0),
        ],
        partialCredit=[
            ContractPartialCredit(label="none", score=0.0, checkIds=[]),
            ContractPartialCredit(label="full", score=2.0, checkIds=["c1", "c2"]),
        ],
        requiresVisual=True,
    )


class TestPathGraphChecker:
    def test_wrong_paths_force_no_on_path_checks(self):
        answer = "Path 1: A-B-G=99. Path 2: X-Y-Z=50. No solutions."
        verdicts = {"c1": "yes", "c2": "yes"}
        adjusted = apply_deterministic_checkers(_r4_item(), answer, verdicts)
        assert adjusted["c1"] == "no"

    def test_all_required_paths_keeps_yes(self):
        answer = "Paths: ABCG=15, DBCG=16, DEFG=17, DHI=19."
        verdicts = {"c1": "yes", "c2": "partial"}
        adjusted = apply_deterministic_checkers(_r4_item(), answer, verdicts)
        assert adjusted["c1"] == "yes"

    def test_wrong_paths_yield_zero_score(self):
        answer = "No network diagram paths listed here."
        verdicts = apply_deterministic_checkers(
            _r4_item(), answer, {"c1": "yes", "c2": "yes"}
        )
        result = compute_score(_r4_item(), verdicts)
        assert result.score == 0.0
