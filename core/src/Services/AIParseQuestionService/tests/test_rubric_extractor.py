"""Unit tests for score-grid post-processing (no OpenAI calls)."""

from app.schemas.rubric import ExtractedQuestion
from app.services.rubric_extractor import (
    collapse_percentage_sections,
    normalize_percentage_groups,
    _round2,
)


def _q(num: str, score: float, group: str, label: str = "") -> ExtractedQuestion:
    return ExtractedQuestion(
        groupLabel=group,
        questionNumber=num,
        label=label or f"Leaf {num}",
        maxScore=score,
        confidence=0.9,
    )


def test_collapse_request_style_over_split():
    """Screenshot case: Request 2/3/5 split into grading-guide bullets."""
    questions = [
        _q("1.1", 2.0, "Request 1 (20%)", "Project name"),
        _q("2.1", 0.3, "Request 2 (20%)", "Current org"),
        _q("2.2", 0.3, "Request 2 (20%)", "Recommended org"),
        _q("2.3", 0.7, "Request 2 (20%)", "Reason current"),
        _q("2.4", 0.7, "Request 2 (20%)", "Reason recommended"),
        _q("3.1", 0.3, "Request 3 (20%)", "Risk 1"),
        _q("3.2", 0.3, "Request 3 (20%)", "Risk 2"),
        _q("3.3", 0.3, "Request 3 (20%)", "Risk 3"),
        _q("3.4", 0.3, "Request 3 (20%)", "Risk details"),
        _q("3.5", 0.2, "Request 3 (20%)", "Clarity"),
        _q("4.1", 1.0, "Request 4 (20%)", "Paths"),
        _q("4.2", 0.5, "Request 4 (20%)", "Solutions"),
        _q("4.3", 0.5, "Request 4 (20%)", "Explain"),
        _q("5.1", 0.5, "Request 5 (20%)", "Calculate"),
        _q("5.2", 0.5, "Request 5 (20%)", "Comment"),
    ]

    collapsed, warnings = collapse_percentage_sections(questions, 10.0)
    assert len(collapsed) == 5
    assert [q.question_number for q in collapsed] == ["1", "2", "3", "4", "5"]
    assert all(q.max_score == 2.0 for q in collapsed)
    assert _round2(sum(q.max_score for q in collapsed)) == 10.0
    assert any("gộp" in w for w in warnings)

    normalized, _ = normalize_percentage_groups(collapsed, 10.0)
    assert _round2(sum(q.max_score for q in normalized)) == 10.0
    assert not any("scaled" in w for w in _)


def test_round2_avoids_float_drift():
    assert _round2(1.9999999999999998) == 2.0
    assert _round2(0.42999999999999994) == 0.43


def test_single_leaf_percent_sections_untouched():
    questions = [
        _q("1", 2.0, "Request 1 (20%)"),
        _q("2", 2.0, "Request 2 (20%)"),
    ]
    collapsed, warnings = collapse_percentage_sections(questions, 10.0)
    assert len(collapsed) == 2
    assert warnings == []
