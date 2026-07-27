"""Grading API endpoint — thin HTTP wrapper over the shared pipeline.

POST /ai/grade/paper — synchronous grading for local/manual testing and debugging.

The production path is event-driven: GradingService publishes ``AiGradeRequested`` on RabbitMQ,
the worker (app/infra/rabbitmq_consumer.py) runs the same pipeline and replies with
``AiGradeCompleted`` / ``AiGradeFailed``. This endpoint returns suggestions inline and does NOT
write back to GradingService.
"""

from __future__ import annotations

import logging

from fastapi import APIRouter, Header, HTTPException

from app.config import settings
from app.schemas.grading import GradePaperRequest, GradePaperResponse
from app.services.pipeline import PipelineError, run_grading_pipeline

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/ai/grade", tags=["grading"])


@router.post("/paper", response_model=GradePaperResponse)
async def grade_paper(
    body: GradePaperRequest,
    x_internal_api_key: str | None = Header(default=None, alias="X-Internal-Api-Key"),
) -> GradePaperResponse:
    """Grade a student paper and return suggestions inline (no write-back)."""

    if not x_internal_api_key or x_internal_api_key != settings.internal_api_key:
        raise HTTPException(status_code=401, detail="Invalid internal API key")

    try:
        result = await run_grading_pipeline(body)
    except PipelineError as exc:
        raise HTTPException(status_code=422, detail=str(exc)) from exc
    except Exception as exc:
        logger.exception("Grading pipeline failed for assignment %s", body.assignment_id)
        raise HTTPException(status_code=500, detail=f"Grading pipeline error: {exc}") from exc

    return GradePaperResponse(
        assignmentId=body.assignment_id,
        suggestions=result.suggestions,
        warnings=result.warnings,
        modelUsed=result.model_used,
        promptVersion=settings.prompt_version,
    )
