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
    """Format a human-readable comment from the suggestion for QuestionComment."""
    parts: list[str] = []

    if suggestion.rationale:
        parts.append(suggestion.rationale)

    angle_parts = []
    for name, angle in suggestion.angles.items():
        angle_parts.append(f"{name}: {angle.s:.1f} — {angle.note}")
    if angle_parts:
        parts.append("\n📊 " + " | ".join(angle_parts))

    if suggestion.evidence:
        evidence_str = "; ".join(f'"{e}"' for e in suggestion.evidence[:3])
        parts.append(f"\n📝 Evidence: {evidence_str}")

    parts.append(f"\n🎯 Confidence: {suggestion.confidence:.0%}")

    if suggestion.flags:
        parts.append(f"\n⚠️ Flags: {', '.join(suggestion.flags)}")

    return "\n".join(parts)
