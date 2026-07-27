"""Pipeline unit tests — non-contract leaves must be manual_only (spec R2 / D2)."""

from __future__ import annotations

from unittest.mock import AsyncMock, patch

import pytest

from app.schemas.grading import GradePaperRequest
from app.services.pipeline import run_grading_pipeline


@pytest.mark.asyncio
async def test_non_contract_leaf_returns_manual_only_without_llm():
    body = GradePaperRequest.model_validate({
        "assignmentId": "a1",
        "subjectId": "s1",
        "rubricVersion": "1",
        "files": [{"url": "http://test/answer.txt", "contentType": "text/plain"}],
        "scoreGrid": [
            {
                "questionNumber": "1",
                "label": "Test",
                "maxScore": 2.0,
            }
        ],
    })

    with patch("app.services.pipeline._download_file", new_callable=AsyncMock) as mock_dl, \
         patch("app.services.pipeline.normalize_txt_cached", new_callable=AsyncMock) as mock_txt, \
         patch("app.services.pipeline._render_student_pages", new_callable=AsyncMock) as mock_render, \
         patch("app.services.pipeline.segment_answers", new_callable=AsyncMock) as mock_seg, \
         patch("app.services.pipeline.grade_contract_leaves", new_callable=AsyncMock) as mock_verdict, \
         patch("app.services.pipeline.get_cached_suggestion", new_callable=AsyncMock) as mock_cache:
        mock_dl.return_value = b"Answer text for question 1"
        mock_txt.return_value = "Request 1: Some answer content here."
        mock_render.return_value = ([], [])
        mock_cache.return_value = None
        mock_seg.return_value = type("Seg", (), {
            "text": lambda self, p: "Some answer content here.",
            "confidence": lambda self, p: 1.0,
        })()
        mock_verdict.return_value = []

        result = await run_grading_pipeline(body)

    mock_verdict.assert_not_called()
    assert len(result.suggestions) == 1
    assert result.suggestions[0].score == 0.0
    assert "manual_only" in result.suggestions[0].flags
    assert "no_contract" in result.suggestions[0].flags
