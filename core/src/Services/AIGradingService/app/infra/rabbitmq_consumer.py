"""RabbitMQ worker — consume AiGradeRequested, grade, reply with AiGradeCompleted/Failed.

Speaks the MassTransit envelope protocol (see app/infra/masstransit_envelope.py) so it interoperates
directly with GradingService's MassTransit bus. Concurrency is bounded by the channel prefetch count
(coarse) and the distributed LLM semaphore inside the pipeline (fine).
"""

from __future__ import annotations

import asyncio
import json
import logging

import aio_pika
from aio_pika.abc import AbstractIncomingMessage, AbstractRobustConnection
from opentelemetry.trace import SpanKind

from app.config import settings
from app.infra.masstransit_envelope import (
    CONTENT_TYPE,
    build_envelope,
    ci_get,
    deterministic_reply_id,
    exchange_name,
    extract_trace_carrier,
    message_of,
    parse_envelope,
)
from app.infra import metrics
from app.infra.redis_cache import claim_once, release_claim
from app.infra.telemetry import extract_context, get_tracer, inject_context
from app.schemas.grading import (
    ContractCheckItem,
    ContractPartialCredit,
    FileRef,
    GradeMode,
    GradePaperRequest,
    ScoreGridItem,
)
from app.services.pipeline import PipelineError, run_grading_pipeline
from app.services.writeback import build_completed_message, build_failed_message

logger = logging.getLogger(__name__)

_REQUEST_EVENT = "AiGradeRequestedEvent"
_COMPLETED_EVENT = "AiGradeCompletedEvent"
_FAILED_EVENT = "AiGradeFailedEvent"
_REQUEST_QUEUE = "ai-grade-requested-ai-service"

# Rubric (re)compilation → invalidate cached suggestions for the affected subject/version.
_RUBRIC_COMPILED_EVENT = "RubricCompiledEvent"
_RUBRIC_COMPILED_QUEUE = "rubric-compiled-ai-service"

_connection: AbstractRobustConnection | None = None
_channel: aio_pika.abc.AbstractRobustChannel | None = None
_completed_exchange: aio_pika.abc.AbstractExchange | None = None
_failed_exchange: aio_pika.abc.AbstractExchange | None = None
_task: asyncio.Task | None = None


def start_consumer_task() -> None:
    """Kick off the consumer in the background so app startup isn't blocked by broker availability."""
    global _task
    if _task is None or _task.done():
        _task = asyncio.create_task(_connect_and_consume())


async def stop_consumer() -> None:
    """Cancel the consumer and close the connection."""
    global _task, _connection
    if _task is not None:
        _task.cancel()
        try:
            await _task
        except (asyncio.CancelledError, Exception):  # noqa: BLE001
            pass
        _task = None
    if _connection is not None:
        await _connection.close()
        _connection = None


async def _connect_and_consume() -> None:
    global _connection, _channel, _completed_exchange, _failed_exchange
    try:
        _connection = await aio_pika.connect_robust(
            host=settings.rabbitmq_host,
            port=settings.rabbitmq_port,
            login=settings.rabbitmq_username,
            password=settings.rabbitmq_password,
            virtualhost=settings.rabbitmq_vhost,
        )
        _channel = await _connection.channel()
        await _channel.set_qos(prefetch_count=max(1, settings.max_concurrent_llm))

        _completed_exchange = await _channel.declare_exchange(
            exchange_name(_COMPLETED_EVENT), aio_pika.ExchangeType.FANOUT, durable=True)
        _failed_exchange = await _channel.declare_exchange(
            exchange_name(_FAILED_EVENT), aio_pika.ExchangeType.FANOUT, durable=True)

        request_exchange = await _channel.declare_exchange(
            exchange_name(_REQUEST_EVENT), aio_pika.ExchangeType.FANOUT, durable=True)
        queue = await _channel.declare_queue(_REQUEST_QUEUE, durable=True)
        await queue.bind(request_exchange)
        await queue.consume(_on_message)

        # Cache-invalidation listener for rubric recompiles.
        rubric_exchange = await _channel.declare_exchange(
            exchange_name(_RUBRIC_COMPILED_EVENT), aio_pika.ExchangeType.FANOUT, durable=True)
        rubric_queue = await _channel.declare_queue(_RUBRIC_COMPILED_QUEUE, durable=True)
        await rubric_queue.bind(rubric_exchange)
        await rubric_queue.consume(_on_rubric_compiled)

        logger.info("AI grading RabbitMQ consumer started (queue=%s)", _REQUEST_QUEUE)
    except asyncio.CancelledError:
        raise
    except Exception:
        logger.exception("Failed to start RabbitMQ consumer")
        raise


async def _on_message(message: AbstractIncomingMessage) -> None:
    try:
        envelope = parse_envelope(message.body)
    except Exception:
        logger.exception("Malformed AiGradeRequested body — discarding")
        await message.ack()
        return

    # Continue the distributed trace started upstream (GradingService) across the RabbitMQ hop.
    carrier = extract_trace_carrier(envelope, dict(message.headers or {}))
    remote_ctx = extract_context(carrier)

    with get_tracer().start_as_current_span(
        "AiGradeRequested process", context=remote_ctx, kind=SpanKind.CONSUMER
    ) as span:
        payload = message_of(envelope)
        request_message_id = ci_get(payload, "messageId")
        inner = ci_get(payload, "request", default={}) or {}
        assignment_id = str(
            ci_get(inner, "assignmentId", "assignment_id", default="")
            or ci_get(payload, "assignmentId", "assignment_id", default="")
        )
        reply_id = deterministic_reply_id(
            assignment_id, str(request_message_id) if request_message_id else None)
        span.set_attribute("messaging.system", "rabbitmq")
        span.set_attribute("grading.assignment_id", assignment_id)

        logger.info("AiGradeRequested received: assignment=%s", assignment_id)
        metrics.papers_received_total.add(1)

        try:
            grade_req = _to_grade_request(inner)
        except Exception as exc:  # noqa: BLE001
            logger.exception("Invalid AiGradeRequested payload for assignment %s", assignment_id)
            metrics.papers_failed_total.add(1)
            await _safe_publish_failed(assignment_id, reply_id, f"Invalid request payload: {exc}")
            await message.ack()
            return

        # Inbox dedupe: claim the message-id before touching the (expensive) LLM pipeline so a
        # broker redelivery does not re-run grading. The claim is released on any failure so a
        # legitimate retry can re-run. Falls open when the message-id is missing or Redis is down.
        dedupe_key = f"ai_grade:{request_message_id}" if request_message_id else None
        if dedupe_key is not None and not await claim_once(dedupe_key):
            logger.info(
                "AiGradeRequested skipped (duplicate): messageId=%s assignment=%s",
                request_message_id, assignment_id)
            span.set_attribute("grading.duplicate_skipped", True)
            metrics.papers_duplicate_skipped_total.add(1)
            await message.ack()
            return

        try:
            result = await run_grading_pipeline(grade_req)
        except PipelineError as exc:
            logger.warning("Grading terminal failure for assignment %s: %s", assignment_id, exc)
            await _release(dedupe_key)
            metrics.papers_failed_total.add(1)
            await _safe_publish_failed(assignment_id, reply_id, str(exc))
            await message.ack()
            return
        except Exception as exc:  # noqa: BLE001
            logger.exception("Grading pipeline crashed for assignment %s", assignment_id)
            await _release(dedupe_key)
            metrics.papers_failed_total.add(1)
            await _safe_publish_failed(assignment_id, reply_id, f"Grading pipeline error: {exc}")
            await message.ack()
            return

        try:
            msg = build_completed_message(
                reply_id, assignment_id, result.suggestions, result.model_used,
                settings.prompt_version, result.paper_comment)
            await _publish(_completed_exchange, _COMPLETED_EVENT, msg, reply_id)
            await message.ack()
            metrics.papers_graded_total.add(1)
            logger.info(
                "AiGradeCompleted published: assignment=%s leaves=%d",
                assignment_id, len(result.suggestions))
        except Exception:
            # Infrastructure failure publishing the result — requeue so it is retried.
            logger.exception(
                "Failed to publish AiGradeCompleted for assignment %s — requeue", assignment_id)
            await _release(dedupe_key)
            await message.nack(requeue=True)


async def _on_rubric_compiled(message: AbstractIncomingMessage) -> None:
    """Invalidate cached suggestions when a subject's compiled rubric changes."""
    try:
        envelope = parse_envelope(message.body)
        payload = message_of(envelope)
        subject_id = str(ci_get(payload, "subjectId", "subject_id", default=""))
        rubric_version = ci_get(payload, "rubricVersion", "rubric_version")
        if subject_id:
            from app.services.cache import invalidate_suggestions

            version = str(rubric_version) if rubric_version is not None else None
            await invalidate_suggestions(subject_id, version)
    except Exception:  # noqa: BLE001
        logger.exception("Failed to process RubricCompiled event")
    finally:
        await message.ack()


async def _release(dedupe_key: str | None) -> None:
    """Release an inbox claim so a retry can re-run; no-op when there is nothing to release."""
    if dedupe_key is not None:
        await release_claim(dedupe_key)


def _to_grade_request(inner: dict) -> GradePaperRequest:
    files = [
        FileRef(
            url=ci_get(f, "url", default=""),
            content_type=ci_get(f, "contentType", "content_type", default="application/octet-stream"),
        )
        for f in (ci_get(inner, "files", default=[]) or [])
    ]
    score_grid = [
        _to_score_grid_item(i)
        for i in (ci_get(inner, "scoreGrid", "score_grid", default=[]) or [])
    ]
    rubric_files = [
        FileRef(
            url=ci_get(f, "url", default=""),
            content_type=ci_get(f, "contentType", "content_type", default="application/octet-stream"),
        )
        for f in (ci_get(inner, "rubricFiles", "rubric_files", default=[]) or [])
    ]
    return GradePaperRequest(
        assignmentId=str(ci_get(inner, "assignmentId", "assignment_id", default="")),
        subjectId=str(ci_get(inner, "subjectId", "subject_id", default="")),
        rubricVersion=str(ci_get(inner, "rubricVersion", "rubric_version", default="1") or "1"),
        files=files,
        scoreGrid=score_grid,
        rubricText=ci_get(inner, "rubricText", "rubric_text"),
        rubricFiles=rubric_files,
        compiledRubricVersion=ci_get(inner, "compiledRubricVersion", "compiled_rubric_version"),
        language=str(ci_get(inner, "language", default="vi") or "vi"),
        studentPageImageUrls=list(ci_get(inner, "studentPageImageUrls", "student_page_image_urls", default=[]) or []),
        mode=GradeMode.BATCH,
    )


def _to_score_grid_item(i: dict) -> ScoreGridItem:
    """Map a score-grid dict (with optional compiled-contract enrichment) to ScoreGridItem."""
    check_items = [
        ContractCheckItem(
            checkId=str(ci_get(c, "checkId", "check_id", default="")),
            description=str(ci_get(c, "description", default="") or ""),
            points=float(ci_get(c, "points", default=0) or 0),
            required=bool(ci_get(c, "required", default=False)),
            evidenceHint=ci_get(c, "evidenceHint", "evidence_hint"),
        )
        for c in (ci_get(i, "checkItems", "check_items", default=[]) or [])
    ]
    partial_credit = [
        ContractPartialCredit(
            label=str(ci_get(t, "label", default="") or ""),
            score=float(ci_get(t, "score", default=0) or 0),
            condition=str(ci_get(t, "condition", default="") or ""),
            checkIds=list(ci_get(t, "checkIds", "check_ids", default=[]) or []),
        )
        for t in (ci_get(i, "partialCredit", "partial_credit", default=[]) or [])
    ]
    return ScoreGridItem(
        question_number=str(ci_get(i, "questionNumber", "question_number", default="")),
        group_label=ci_get(i, "groupLabel", "group_label"),
        label=ci_get(i, "label"),
        max_score=float(ci_get(i, "maxScore", "max_score", default=0) or 0),
        parent_question=ci_get(i, "parentQuestion", "parent_question"),
        scoring_rule=ci_get(i, "scoringRule", "scoring_rule"),
        answer_key=ci_get(i, "answerKey", "answer_key"),
        question_text=ci_get(i, "questionText", "question_text"),
        check_items=check_items,
        partial_credit=partial_credit,
        common_mistakes=list(ci_get(i, "commonMistakes", "common_mistakes", default=[]) or []),
        not_penalize=list(ci_get(i, "notPenalize", "not_penalize", default=[]) or []),
        requires_visual=bool(ci_get(i, "requiresVisual", "requires_visual", default=False)),
        visual_asset_urls=list(ci_get(i, "visualAssetUrls", "visual_asset_urls", default=[]) or []),
        specificity=ci_get(i, "specificity"),
        extraction_confidence=ci_get(i, "extractionConfidence", "extraction_confidence"),
    )


async def _publish(
    exchange: aio_pika.abc.AbstractExchange | None,
    type_name: str,
    message: dict,
    message_id: str,
) -> None:
    if exchange is None:
        raise RuntimeError("RabbitMQ publish exchange not initialised")
    # Carry the active trace context so the .NET consumer links its span to this one.
    trace_headers = inject_context({})
    envelope = build_envelope(type_name, message, message_id, headers=trace_headers)
    body = json.dumps(envelope).encode("utf-8")
    await exchange.publish(
        aio_pika.Message(
            body=body,
            content_type=CONTENT_TYPE,
            message_id=message_id,
            headers=trace_headers or None,
            delivery_mode=aio_pika.DeliveryMode.PERSISTENT,
        ),
        routing_key="",
    )


async def _safe_publish_failed(assignment_id: str, reply_id: str, reason: str) -> None:
    """Publish AiGradeFailed, swallowing broker errors so we can still ack (avoids poison loops)."""
    try:
        msg = build_failed_message(reply_id, assignment_id, reason)
        await _publish(_failed_exchange, _FAILED_EVENT, msg, reply_id)
        logger.info("AiGradeFailed published: assignment=%s", assignment_id)
    except Exception:
        logger.exception("Failed to publish AiGradeFailed for assignment %s", assignment_id)
