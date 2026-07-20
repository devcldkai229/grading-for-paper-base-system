"""Grading API endpoint — orchestrates the full pipeline.

POST /ai/grade/paper — Sync Suggest mode (Slice 1)

Pipeline:
  1. Download file(s) → Normalise (txt)
  2. Segment by **parent** question → map answers
  3. Route per **leaf** criterion (empty_check / llm_text) using parent answer
  4. Cache check → return cached suggestions immediately
  5. Grade cache-miss leaves (single T1 LLM call)
  6. Verify → clamp, confidence gate, evidence grounding, injection scan
  7. Cache write (verified suggestions)
  8. Write-back → GradingService (best-effort)
  9. Return GradePaperResponse
"""

from __future__ import annotations

import asyncio
import logging
import time

import httpx
from fastapi import APIRouter, Header, HTTPException

from app.config import settings
from app.schemas.grading import (
    AngleScore,
    GradeMode,
    GradePaperRequest,
    GradePaperResponse,
    QuestionSuggestion,
)
from app.services.cache import get_cached_suggestion, set_cached_suggestion
from app.services.grader import grade_with_llm
from app.services.normalize.docx import normalize_docx
from app.services.normalize.pdf import normalize_pdf
from app.services.normalize.txt import normalize_txt_cached
from app.services.router import Evaluator, EvalTask, resolve_parent, route_questions
from app.services.segmenter import segment_answers
from app.services.verifier import verify_suggestions
from app.services.writeback import write_back_suggestions

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/ai/grade", tags=["grading"])


@router.post("/paper", response_model=GradePaperResponse)
async def grade_paper(
    body: GradePaperRequest,
    x_internal_api_key: str | None = Header(default=None, alias="X-Internal-Api-Key"),
) -> GradePaperResponse:
    """Grade a student paper (sync suggest mode)."""

    if not x_internal_api_key or x_internal_api_key != settings.internal_api_key:
        raise HTTPException(status_code=401, detail="Invalid internal API key")

    if body.mode == GradeMode.BATCH:
        raise HTTPException(
            status_code=501,
            detail="Batch mode not yet implemented (Slice 4)",
        )

    start = time.monotonic()
    warnings: list[str] = []
    model_used = settings.openai_model_t1

    try:
        # ===== Step 1: Download & Normalise (parallel) =====
        download_tasks = [_download_file(f.url) for f in body.files]
        downloaded = await asyncio.gather(*download_tasks, return_exceptions=True)

        all_text_parts: list[str] = []
        for file_ref, result in zip(body.files, downloaded):
            if isinstance(result, Exception):
                warnings.append(f"Failed to download file: {result}")
                continue

            data = result
            ct = file_ref.content_type.lower()

            try:
                if "text/" in ct or ct.endswith(".txt"):
                    text = await normalize_txt_cached(data)
                elif "pdf" in ct or file_ref.url.lower().endswith(".pdf"):
                    text = normalize_pdf(data)
                elif "word" in ct or "docx" in ct or file_ref.url.lower().endswith(".docx"):
                    text = normalize_docx(data)
                else:
                    warnings.append(
                        f"Unsupported content type '{file_ref.content_type}' — file skipped."
                    )
                    continue
            except Exception as exc:
                warnings.append(f"Failed to normalize '{file_ref.content_type}': {exc}")
                continue

            if text.strip():
                all_text_parts.append(text)

        if not all_text_parts:
            raise HTTPException(
                status_code=422,
                detail="No supported files to grade (txt, pdf, docx)",
            )

        full_text = "\n\n".join(all_text_parts)

        # ===== Step 2: Segment by parent questions =====
        parent_questions: list[str] = []
        seen_parents: set[str] = set()
        for item in body.score_grid:
            parent = resolve_parent(item)
            if parent not in seen_parents:
                seen_parents.add(parent)
                parent_questions.append(parent)

        segments = await segment_answers(full_text, parent_questions)

        # Leaf → parent answer (for verifier / cache)
        leaf_answers: dict[str, str] = {}
        for item in body.score_grid:
            parent = resolve_parent(item)
            leaf_answers[item.question_number] = segments.get(parent, "")

        # ===== Step 3: Route per leaf =====
        tasks = route_questions(segments, body.score_grid)

        # ===== Step 4: Cache check + empty results =====
        all_suggestions: list[QuestionSuggestion] = []
        cache_miss_tasks: list[EvalTask] = []

        for task in tasks:
            if task.evaluator == Evaluator.EMPTY_CHECK:
                suggestion = _empty_suggestion(task)
                all_suggestions.append(suggestion)
                continue

            cached = await get_cached_suggestion(
                body.subject_id, body.rubric_version,
                task.question_number, task.answer_text,
            )
            if cached is not None:
                all_suggestions.append(cached)
            else:
                cache_miss_tasks.append(task)

        cache_hits = len(tasks) - len(cache_miss_tasks) - sum(
            1 for t in tasks if t.evaluator == Evaluator.EMPTY_CHECK
        )
        if cache_hits > 0:
            logger.info("Cache hits: %d leaf criteria", cache_hits)

        # ===== Step 5: Grade cache misses =====
        if cache_miss_tasks:
            llm_suggestions = await grade_with_llm(
                cache_miss_tasks,
                model_used,
                rubric_text=body.rubric_text,
            )

            llm_map = {s.question_number: s for s in llm_suggestions}
            for task in cache_miss_tasks:
                suggestion = llm_map.get(task.question_number)
                if suggestion is None:
                    suggestion = _manual_fallback_suggestion(task)
                    warnings.append(
                        f"Q{task.question_number}: LLM did not return a suggestion → MANUAL_ONLY"
                    )
                all_suggestions.append(suggestion)

        # ===== Step 6: Verify =====
        all_suggestions, verify_warnings = verify_suggestions(
            all_suggestions, body.score_grid, leaf_answers,
        )
        warnings.extend(verify_warnings)

        # ===== Step 7: Cache write =====
        for suggestion in all_suggestions:
            if "manual_only" not in suggestion.flags:
                q_num = suggestion.question_number
                answer = leaf_answers.get(q_num, "")
                if answer.strip():
                    await set_cached_suggestion(
                        body.subject_id, body.rubric_version,
                        q_num, answer, suggestion,
                    )

        # ===== Step 8: Write-back =====
        write_back_ok = await write_back_suggestions(
            body.assignment_id, all_suggestions, model_used, settings.prompt_version,
        )
        if not write_back_ok:
            warnings.append("Write-back to GradingService failed — suggestions returned inline only")

        elapsed = time.monotonic() - start
        logger.info(
            "Grading complete: assignment=%s, leaves=%d, elapsed=%.2fs",
            body.assignment_id, len(all_suggestions), elapsed,
        )

        return GradePaperResponse(
            assignmentId=body.assignment_id,
            suggestions=all_suggestions,
            warnings=warnings,
            modelUsed=model_used,
            promptVersion=settings.prompt_version,
        )

    except HTTPException:
        raise
    except Exception as exc:
        logger.exception("Grading pipeline failed for assignment %s", body.assignment_id)
        raise HTTPException(
            status_code=500,
            detail=f"Grading pipeline error: {exc}",
        ) from exc


async def _download_file(url: str) -> bytes:
    """Download a file from a presigned URL."""
    async with httpx.AsyncClient(timeout=60) as client:
        resp = await client.get(url)
        resp.raise_for_status()
        return resp.content


def _empty_suggestion(task: EvalTask) -> QuestionSuggestion:
    """Create a zero-score suggestion for a blank/missing answer."""
    flag_note = "missing" if "missing" in task.flags else "blank"
    return QuestionSuggestion(
        questionNumber=task.question_number,
        angles={
            "correctness": AngleScore(s=0, note="No answer provided"),
            "completeness": AngleScore(s=0, note="No answer provided"),
            "relevance": AngleScore(s=0, note="No answer provided"),
            "clarity": AngleScore(s=0, note="No answer provided"),
        },
        score=0.0,
        rationale=f"Student did not provide an answer for parent question {task.parent_question} ({flag_note}).",
        evidence=[],
        confidence=1.0,
        flags=list(dict.fromkeys(task.flags + ["manual_only"])),
    )


def _manual_fallback_suggestion(task: EvalTask) -> QuestionSuggestion:
    """Create a MANUAL_ONLY fallback when LLM fails to return a suggestion."""
    return QuestionSuggestion(
        questionNumber=task.question_number,
        angles={
            "correctness": AngleScore(s=0, note="Could not be evaluated by AI"),
            "completeness": AngleScore(s=0, note="Could not be evaluated by AI"),
            "relevance": AngleScore(s=0, note="Could not be evaluated by AI"),
            "clarity": AngleScore(s=0, note="Could not be evaluated by AI"),
        },
        score=0.0,
        rationale="AI could not grade this criterion. Manual review required.",
        evidence=[],
        confidence=0.0,
        flags=["manual_only", "llm_fallback_failed"],
    )
