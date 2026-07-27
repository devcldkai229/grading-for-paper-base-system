"""Tests for deterministic score computation (doc 5.5 / B6).

The score must be a pure function of the adjudicated verdicts + the compiled partial-credit tiers —
the LLM never picks the number. These tests pin that contract using the real PMG201c Request 3
barem: *"Mỗi rủi ro với một mô tả cụ thể và phù hợp với dự án được 0.3 điểm, cung cấp các thông tin
cụ thể (probability, impact, response plan) được thêm 0.3 điểm. Trình bày rõ ràng thêm tối đa 0.2.
Tổng không vượt quá 2 điểm."*
"""

from __future__ import annotations

from app.schemas.grading import ContractCheckItem, ContractPartialCredit, ScoreGridItem
from app.services.scoring import compute_score


def _r3_item() -> ScoreGridItem:
    """PMG201c Request 3 compiled into check-items + tiers."""
    return ScoreGridItem(
        questionNumber="3",
        maxScore=2.0,
        checkItems=[
            ContractCheckItem(checkId="c1", description="Nêu >= 3 rủi ro cụ thể", points=0.9, required=True),
            ContractCheckItem(checkId="c2", description="Mỗi rủi ro có probability + impact", points=0.6),
            ContractCheckItem(checkId="c3", description="Mỗi rủi ro có response plan", points=0.3),
            ContractCheckItem(checkId="c4", description="Trình bày rõ ràng", points=0.2),
        ],
        partialCredit=[
            ContractPartialCredit(label="none", score=0.0, checkIds=[]),
            ContractPartialCredit(label="minimal", score=0.9, checkIds=["c1"]),
            ContractPartialCredit(label="partial", score=1.5, checkIds=["c1", "c2"]),
            ContractPartialCredit(label="high", score=1.8, checkIds=["c1", "c2", "c3"]),
            ContractPartialCredit(label="full", score=2.0, checkIds=["c1", "c2", "c3", "c4"]),
        ],
    )


class TestTierSelection:
    def test_all_checks_met_awards_full_tier(self):
        r = compute_score(_r3_item(), {"c1": "yes", "c2": "yes", "c3": "yes", "c4": "yes"})
        assert r.score == 2.0
        assert r.tier_label == "full"

    def test_real_student_case_risks_without_response_plan(self):
        """The actual paper: 4 risks described, but no probability/impact/response → NOT full marks.

        This is the regression for the observed failure where the AI awarded 2.00/2.00.
        """
        r = compute_score(_r3_item(), {"c1": "yes", "c2": "no", "c3": "no", "c4": "partial"})
        assert r.score == 0.9
        assert r.tier_label == "minimal"
        assert r.score < 2.0

    def test_highest_qualifying_tier_wins(self):
        r = compute_score(_r3_item(), {"c1": "yes", "c2": "yes", "c3": "no", "c4": "yes"})
        assert r.tier_label == "partial"
        assert r.score == 1.5

    def test_no_tier_qualifies_falls_back_to_lowest(self):
        r = compute_score(_r3_item(), {"c1": "no", "c2": "no", "c3": "no", "c4": "no"})
        assert r.score == 0.0
        assert r.tier_label == "none"

    def test_partial_verdict_does_not_satisfy_a_tier_but_sums_points(self):
        """'partial' is not 'yes' for tier membership — fall back to point sum instead of 0."""
        r = compute_score(_r3_item(), {"c1": "partial", "c2": "yes", "c3": "yes", "c4": "yes"})
        assert r.tier_label == "Tổng hợp điểm thành phần"
        # 0.5*0.9 + 0.6 + 0.3 + 0.2 = 1.55
        assert r.score == 1.55

    def test_unclear_required_falls_back_to_earned_points(self):
        r = compute_score(_r3_item(), {"c1": "unclear", "c2": "yes", "c3": "yes", "c4": "yes"})
        assert r.tier_label == "Tổng hợp điểm thành phần"
        assert r.score == 1.1  # 0.6+0.3+0.2

    def test_mixed_yes_partial_does_not_collapse_to_no_credit(self):
        """Regression: Q1-style 3 yes + required partial must not become No Credit 0."""
        item = ScoreGridItem(
            questionNumber="1",
            maxScore=2.0,
            checkItems=[
                ContractCheckItem(checkId="1", points=0.5, required=True),
                ContractCheckItem(checkId="2", points=0.5, required=True),
                ContractCheckItem(checkId="3", points=0.5, required=True),
                ContractCheckItem(checkId="4", points=0.5, required=True),
                ContractCheckItem(checkId="5", points=0.5, required=False),
            ],
            partialCredit=[
                ContractPartialCredit(label="Full", score=2.0, checkIds=["1", "2", "3", "4", "5"]),
                ContractPartialCredit(label="Partial", score=1.5, checkIds=["1", "2", "3", "4"]),
                ContractPartialCredit(label="No Credit", score=0.0, checkIds=[]),
            ],
        )
        r = compute_score(item, {"1": "yes", "2": "partial", "3": "yes", "4": "yes", "5": "partial"})
        assert r.score == 2.0
        assert r.tier_label == "Tổng hợp điểm thành phần"
        assert r.score > 0

class TestDeterminism:
    def test_same_verdicts_always_produce_same_score(self):
        verdicts = {"c1": "yes", "c2": "yes", "c3": "no", "c4": "partial"}
        scores = {compute_score(_r3_item(), verdicts).score for _ in range(20)}
        assert len(scores) == 1

    def test_verdict_order_does_not_matter(self):
        a = compute_score(_r3_item(), {"c1": "yes", "c2": "yes", "c3": "no", "c4": "no"})
        b = compute_score(_r3_item(), {"c4": "no", "c3": "no", "c2": "yes", "c1": "yes"})
        assert a.score == b.score and a.tier_label == b.tier_label


class TestReporting:
    def test_reports_satisfied_partial_and_missing_required(self):
        r = compute_score(_r3_item(), {"c1": "no", "c2": "yes", "c3": "partial", "c4": "yes"})
        assert r.satisfied == ["c2", "c4"]
        assert r.partial == ["c3"]
        assert r.missing_required == ["c1"]  # c1 is required and unmet

    def test_missing_verdict_counts_as_unmet(self):
        r = compute_score(_r3_item(), {})
        assert r.score == 0.0
        assert r.missing_required == ["c1"]


class TestNoTierFallback:
    def _item_without_tiers(self) -> ScoreGridItem:
        return ScoreGridItem(
            questionNumber="1",
            maxScore=2.0,
            checkItems=[
                ContractCheckItem(checkId="c1", points=0.5),
                ContractCheckItem(checkId="c2", points=0.5),
                ContractCheckItem(checkId="c3", points=0.5),
                ContractCheckItem(checkId="c4", points=0.5),
            ],
        )

    def test_sums_points_for_yes(self):
        r = compute_score(self._item_without_tiers(), {"c1": "yes", "c2": "yes"})
        assert r.score == 1.0

    def test_partial_counts_half(self):
        r = compute_score(self._item_without_tiers(), {"c1": "yes", "c2": "partial"})
        assert r.score == 0.75


class TestBounds:
    def test_score_never_exceeds_max(self):
        item = _r3_item()
        item.partial_credit = [ContractPartialCredit(label="bug", score=99.0, checkIds=["c1"])]
        r = compute_score(item, {"c1": "yes"})
        assert r.score == item.max_score

    def test_score_never_negative(self):
        item = _r3_item()
        item.partial_credit = [ContractPartialCredit(label="bug", score=-5.0, checkIds=["c1"])]
        r = compute_score(item, {"c1": "yes"})
        assert r.score == 0.0
