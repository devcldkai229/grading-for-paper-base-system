"""Golden-set scoring tests for PMG201c (deterministic layer — no LLM required)."""

from __future__ import annotations

import json
from pathlib import Path

import pytest

from app.schemas.grading import ScoreGridItem
from app.services.checkers import apply_deterministic_checkers
from app.services.scoring import compute_score

GOLDEN_PATH = Path(__file__).resolve().parent.parent / "eval" / "golden" / "pmg201c_pe1.json"


@pytest.fixture
def pmg201c_paper() -> dict:
    data = json.loads(GOLDEN_PATH.read_text(encoding="utf-8"))
    return data["papers"][0]


class TestPmg201cDeterministicScoring:
    def test_r3_no_response_plan_not_full_marks(self, pmg201c_paper: dict):
        item = ScoreGridItem.model_validate(pmg201c_paper["score_grid"][0])
        verdicts = pmg201c_paper["expected_verdicts"]["3"]
        result = compute_score(item, verdicts)
        assert result.score == pytest.approx(0.9)
        assert result.score < 2.0

    def test_r4_wrong_paths_score_zero_with_checker(self, pmg201c_paper: dict):
        item = ScoreGridItem.model_validate(pmg201c_paper["score_grid"][1])
        verdicts = apply_deterministic_checkers(
            item, "No valid network paths in this answer.", {"c1": "yes", "c2": "yes"}
        )
        result = compute_score(item, verdicts)
        assert result.score == 0.0

    def test_r5_blank_scores_zero(self, pmg201c_paper: dict):
        item = ScoreGridItem.model_validate(pmg201c_paper["score_grid"][2])
        verdicts = pmg201c_paper["expected_verdicts"]["5"]
        result = compute_score(item, verdicts)
        assert result.score == 0.0

    def test_total_approx_three_of_ten(self, pmg201c_paper: dict):
        total = 0.0
        for grid_item, qn in zip(pmg201c_paper["score_grid"], ["3", "4", "5"]):
            item = ScoreGridItem.model_validate(grid_item)
            verdicts = dict(pmg201c_paper["expected_verdicts"][qn])
            total += compute_score(item, verdicts).score
        assert total == pytest.approx(0.9, abs=0.5)
