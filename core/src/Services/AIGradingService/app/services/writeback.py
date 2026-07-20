"""Write-back client — push AI grading results to GradingService.

Calls the GradingService internal API to update QuestionGradeDetail
(AiDrafted=true, Score, QuestionComment) and GradingAssignment.AiStatus.

If no write-back endpoint is configured or the call fails, results are still
returned to the caller — write-back is best-effort, never blocks grading output.
"""

from __future__ import annotations

import logging
from typing import Any

import httpx

from app.config import settings
from app.schemas.grading import QuestionSuggestion

logger = logging.getLogger(__name__)

_WRITE_BACK_TIMEOUT = 30  # seconds
_http_client: httpx.AsyncClient | None = None


def _get_http_client() -> httpx.AsyncClient:
    global _http_client
    if _http_client is None:
        _http_client = httpx.AsyncClient(timeout=_WRITE_BACK_TIMEOUT)
    return _http_client


async def write_back_suggestions(
    assignment_id: str,
    suggestions: list[QuestionSuggestion],
    model_used: str,
    prompt_version: str,
) -> bool:
    """Push grading suggestions to GradingService.

    Args:
        assignment_id: GradingAssignment.Id
        suggestions: Verified suggestions from the pipeline.
        model_used: Which LLM model was used.
        prompt_version: Prompt version for audit trail.

    Returns:
        True if write-back succeeded, False otherwise.
    """
    base_url = settings.grading_service_base_url
    if not base_url:
        logger.warning("GRADING_SERVICE_BASE_URL not configured — skipping write-back")
        return False

    # Build payload matching GradingService expectations
    payload = _build_payload(assignment_id, suggestions, model_used, prompt_version)

    url = f"{base_url.rstrip('/')}/api/internal/ai/ai-suggestions"

    for attempt in range(3):
        try:
            client = _get_http_client()
            resp = await client.post(
                url,
                json=payload,
                headers={
                    "X-Internal-Api-Key": settings.internal_api_key,
                    "Content-Type": "application/json",
                },
            )

            if resp.status_code in (200, 201, 204):
                logger.info(
                    "Write-back succeeded for assignment %s (%d suggestions)",
                    assignment_id,
                    len(suggestions),
                )
                return True

            logger.warning(
                "Write-back failed (attempt %d/3): %d %s",
                attempt + 1,
                resp.status_code,
                resp.text[:200],
            )

        except httpx.HTTPError as exc:
            logger.warning(
                "Write-back HTTP error (attempt %d/3): %s",
                attempt + 1,
                exc,
            )
        except Exception as exc:
            logger.error(
                "Write-back unexpected error (attempt %d/3): %s",
                attempt + 1,
                exc,
            )

    logger.error(
        "Write-back FAILED after 3 attempts for assignment %s", assignment_id
    )
    return False


def _build_payload(
    assignment_id: str,
    suggestions: list[QuestionSuggestion],
    model_used: str,
    prompt_version: str,
) -> dict[str, Any]:
    """Build the write-back payload."""
    # Build payload matching ApplyAiSuggestionsRequest (camelCase JSON)
    question_grades = []
    for s in suggestions:
        is_manual = "manual_only" in s.flags
        question_grades.append({
            "questionNumber": s.question_number,
            "score": s.score,
            "questionComment": _format_comment(s),
            "confidence": s.confidence,
            "isManualOnly": is_manual,
        })

    return {
        "assignmentId": assignment_id,
        "questionGrades": question_grades,
        "modelUsed": model_used,
        "promptVersion": prompt_version,
    }


def _format_comment(suggestion: QuestionSuggestion) -> str:
    """Format a human-readable comment from the suggestion for QuestionComment."""
    parts: list[str] = []

    # Rationale
    if suggestion.rationale:
        parts.append(suggestion.rationale)

    # Angle summary
    angle_parts = []
    for name, angle in suggestion.angles.items():
        angle_parts.append(f"{name}: {angle.s:.1f} — {angle.note}")
    if angle_parts:
        parts.append("\n📊 " + " | ".join(angle_parts))

    # Evidence
    if suggestion.evidence:
        evidence_str = "; ".join(f'"{e}"' for e in suggestion.evidence[:3])
        parts.append(f"\n📝 Evidence: {evidence_str}")

    # Confidence
    parts.append(f"\n🎯 Confidence: {suggestion.confidence:.0%}")

    # Flags
    if suggestion.flags:
        parts.append(f"\n⚠️ Flags: {', '.join(suggestion.flags)}")

    return "\n".join(parts)
