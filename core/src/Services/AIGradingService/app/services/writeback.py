"""Build the result payloads sent back to GradingService as MassTransit events.

The AI worker no longer calls GradingService over HTTP. Instead it publishes
``AiGradeCompletedEvent`` (or ``AiGradeFailedEvent``) on RabbitMQ, which GradingService's
consumers apply as the single writer of AiStatus. This module builds the ``message`` payloads
(camelCase, matching the .NET records) — the actual publish lives in the RabbitMQ consumer.
"""

from __future__ import annotations

from datetime import datetime, timezone
from typing import Any

from app.schemas.grading import QuestionSuggestion


def build_completed_message(
    message_id: str,
    assignment_id: str,
    suggestions: list[QuestionSuggestion],
    model_used: str,
    prompt_version: str,
    paper_comment: str = "",
) -> dict[str, Any]:
    """Build the AiGradeCompletedEvent ``message`` payload (camelCase)."""
    question_grades = []
    for s in suggestions:
        question_grades.append({
            "questionNumber": s.question_number,
            "score": s.score,
            "questionComment": _format_comment(s),
            "confidence": s.confidence,
            "isManualOnly": "manual_only" in s.flags,
        })

    return {
        "messageId": message_id,
        "assignmentId": assignment_id,
        "questionGrades": question_grades,
        "modelUsed": model_used,
        "promptVersion": prompt_version,
        "correlationId": None,
        "occurredAt": datetime.now(timezone.utc).isoformat(),
        "paperComment": paper_comment or None,
    }


def build_failed_message(
    message_id: str,
    assignment_id: str,
    reason: str,
) -> dict[str, Any]:
    """Build the AiGradeFailedEvent ``message`` payload (camelCase)."""
    return {
        "messageId": message_id,
        "assignmentId": assignment_id,
        "reason": reason[:1000],
        "correlationId": None,
        "occurredAt": datetime.now(timezone.utc).isoformat(),
    }


def _format_comment(suggestion: QuestionSuggestion) -> str:
    """Format a teacher-facing comment — plain language, no technical angle metrics."""
    parts: list[str] = []

    if suggestion.rationale:
        parts.append(suggestion.rationale)

    if suggestion.evidence:
        evidence_str = "; ".join(f'"{e}"' for e in suggestion.evidence[:3])
        parts.append(f"Minh chứng: {evidence_str}")

    if "manual_only" in (suggestion.flags or []):
        parts.append("AI chưa tự tin — nên chấm tay câu này.")

    return "\n".join(parts) if parts else "Không có nhận xét."
