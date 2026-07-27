"""Grading pipeline — the transport-agnostic core shared by the HTTP endpoint and the
RabbitMQ worker.

Pipeline:
  1. Download file(s) -> Normalise (txt / pdf / docx) + render page images for multimodal
  2. Segment by **parent** question -> map answers
  3. Route per **leaf** criterion (empty_check / contract verdict path)
  4. Cache check -> return cached suggestions immediately
  5. Grade contract leaves (verdict path — code-computed scores)
  6. Verify -> clamp, confidence gate, evidence grounding, injection scan
  7. Cache write (verified suggestions)

Non-contract leaves return manual_only (legacy LLM scorer removed per spec R2).

Write-back / event publishing is intentionally NOT part of this module — callers decide how
to deliver the result (HTTP response vs AiGradeCompletedEvent).
"""

from __future__ import annotations

import asyncio
import logging
import time
from dataclasses import dataclass, field

import httpx

from app.config import settings
from app.schemas.grading import (
    AngleScore,
    FileRef,
    GradePaperRequest,
    QuestionSuggestion,
    ScoreGridItem,
)
from app.services.cache import get_cached_suggestion, set_cached_suggestion
from app.services.coverage import enforce_full_coverage
from app.services.language import ensure_vietnamese
from app.services.normalize.docx import normalize_docx
from app.services.normalize.pdf import normalize_pdf
from app.services.normalize.renderer import render_document
from app.services.normalize.txt import normalize_txt_cached
from app.services.router import Evaluator, EvalTask, resolve_parent, route_questions
from app.services.segmenter import segment_answers
from app.services.verdict_pipeline import grade_contract_leaves, load_images_from_urls
from app.services.verifier import verify_suggestions

logger = logging.getLogger(__name__)


class PipelineError(Exception):
    """Terminal grading failure (e.g. no usable files) — caller should report failure."""


@dataclass
class PipelineResult:
    suggestions: list[QuestionSuggestion]
    warnings: list[str] = field(default_factory=list)
    model_used: str = ""
    paper_comment: str = ""


def _is_renderable(content_type: str, url: str) -> bool:
    ct = content_type.lower()
    lower_url = url.lower()
    return (
        "pdf" in ct
        or "word" in ct
        or "docx" in ct
        or lower_url.endswith(".pdf")
        or lower_url.endswith(".docx")
        or "image/" in ct
        or lower_url.endswith((".png", ".jpg", ".jpeg", ".webp"))
    )


async def run_grading_pipeline(body: GradePaperRequest) -> PipelineResult:
    """Run the full grading pipeline for a single paper. Raises PipelineError on terminal failure."""
    start = time.monotonic()
    warnings: list[str] = []
    model_used = settings.openai_model_vision

    # ===== Step 1: Download & Normalise (parallel) =====
    all_text_parts, student_warnings = await _download_and_normalize_files(body.files)
    warnings.extend(student_warnings)

    if not all_text_parts:
        raise PipelineError("No supported files to grade (txt, pdf, docx)")

    full_text = "\n\n".join(all_text_parts)

    # Render student submission pages locally (300 DPI) for multimodal verdict grading.
    student_pages, render_warnings = await _render_student_pages(body.files)
    warnings.extend(render_warnings)
    if body.student_page_image_urls:
        url_pages = await load_images_from_urls(body.student_page_image_urls)
        student_pages.extend(url_pages)

    # ===== Step 2: Segment by parent questions =====
    parent_questions: list[str] = []
    seen_parents: set[str] = set()
    for item in body.score_grid:
        parent = resolve_parent(item)
        if parent not in seen_parents:
            seen_parents.add(parent)
            parent_questions.append(parent)

    question_texts: dict[str, str] = {}
    for item in body.score_grid:
        parent = resolve_parent(item)
        if parent not in question_texts and (item.question_text or item.label):
            question_texts[parent] = item.question_text or item.label or ""

    segmentation = await segment_answers(full_text, parent_questions, question_texts)

    leaf_answers: dict[str, str] = {}
    for item in body.score_grid:
        parent = resolve_parent(item)
        leaf_answers[item.question_number] = segmentation.text(parent)

    # ===== Step 3: Route per leaf =====
    tasks = route_questions(segmentation, body.score_grid)
    item_by_num = {item.question_number: item for item in body.score_grid}

    # ===== Step 4: Cache check + empty / manual-only results =====
    all_suggestions: list[QuestionSuggestion] = []
    contract_miss_items: list[ScoreGridItem] = []
    manual_only_count = 0

    for task in tasks:
        if task.evaluator == Evaluator.EMPTY_CHECK:
            all_suggestions.append(_empty_suggestion(task))
            continue

        cached = await get_cached_suggestion(
            body.subject_id, body.rubric_version,
            task.question_number, task.answer_text,
        )
        if cached is not None:
            all_suggestions.append(cached)
            continue

        item = item_by_num.get(task.question_number)
        if item is not None and item.has_contract:
            contract_miss_items.append(item)
        else:
            all_suggestions.append(_manual_only_suggestion(task, item))
            manual_only_count += 1

    if manual_only_count:
        warnings.append(
            f"{manual_only_count} tiêu chí không có check-items trong hợp đồng — đánh dấu chấm tay."
        )

    # ===== Step 5: Verdict path for contract leaves (code-computed scores) =====
    if contract_miss_items:
        seg_conf = {
            item.question_number: segmentation.confidence(resolve_parent(item))
            for item in contract_miss_items
        }
        verdict_suggestions = await grade_contract_leaves(
            contract_miss_items,
            leaf_answers,
            student_pages,
            seg_confidence=seg_conf,
        )
        all_suggestions.extend(verdict_suggestions)

    # ===== Step 6: Verify =====
    all_suggestions, verify_warnings = verify_suggestions(
        all_suggestions, body.score_grid, leaf_answers,
    )
    warnings.extend(verify_warnings)

    # ===== Step 6b: Vietnamese feedback =====
    all_suggestions, lang_warnings = await ensure_vietnamese(all_suggestions, model_used)
    warnings.extend(lang_warnings)

    # ===== Coverage invariant: exactly one result per score-grid leaf =====
    all_suggestions, cov_warnings = enforce_full_coverage(all_suggestions, body.score_grid)
    warnings.extend(cov_warnings)
    assert len(all_suggestions) == len(body.score_grid), (
        f"Coverage invariant violated: {len(all_suggestions)} suggestions "
        f"!= {len(body.score_grid)} score-grid leaves"
    )

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

    paper_comment = _build_paper_comment(all_suggestions, body.score_grid)

    elapsed = time.monotonic() - start
    logger.info(
        "Grading complete: assignment=%s, leaves=%d, elapsed=%.2fs",
        body.assignment_id, len(all_suggestions), elapsed,
    )

    return PipelineResult(
        suggestions=all_suggestions,
        warnings=warnings,
        model_used=model_used,
        paper_comment=paper_comment,
    )


def _build_paper_comment(
    suggestions: list[QuestionSuggestion], score_grid: list[ScoreGridItem]
) -> str:
    """Build a concise Vietnamese overall comment from the per-leaf suggestions."""
    if not suggestions:
        return ""

    max_by = {item.question_number: item.max_score for item in score_grid}
    total = sum(s.score for s in suggestions)
    total_max = sum(max_by.get(s.question_number, 0.0) for s in suggestions)

    manual = [s.question_number for s in suggestions if "manual_only" in s.flags]
    low_conf = [
        s.question_number
        for s in suggestions
        if "manual_only" not in s.flags and s.confidence < settings.confidence_threshold
    ]

    parts = [f"Điểm đề xuất: {total:.2f}/{total_max:.2f} trên {len(suggestions)} tiêu chí."]
    if manual:
        parts.append(f"Cần chấm tay: {', '.join(manual)}.")
    if low_conf:
        parts.append(f"Độ tin cậy thấp, nên kiểm tra lại: {', '.join(low_conf)}.")
    parts.append("Đây là gợi ý của AI — vui lòng rà soát trước khi áp dụng.")
    return " ".join(parts)


async def _render_student_pages(files: list[FileRef]) -> tuple[list[bytes], list[str]]:
    """Rasterise student submission files to 300 DPI page PNGs for multimodal grading."""
    warnings: list[str] = []
    page_images: list[bytes] = []

    if not files:
        return page_images, warnings

    downloaded = await asyncio.gather(
        *[_download_file(f.url) for f in files], return_exceptions=True
    )

    for file_ref, result in zip(files, downloaded):
        if isinstance(result, Exception):
            warnings.append(f"Failed to download file for rendering: {result}")
            continue
        if not _is_renderable(file_ref.content_type, file_ref.url):
            continue
        try:
            rendered = await render_document(result, file_ref.content_type, file_ref.url)
            page_images.extend(p.image_png for p in rendered.pages)
        except Exception as exc:
            warnings.append(f"Failed to render '{file_ref.content_type}': {exc}")

    if page_images:
        logger.info("Rendered %d student page image(s) for multimodal grading", len(page_images))
    return page_images, warnings


async def _download_and_normalize_files(files) -> tuple[list[str], list[str]]:
    """Download + normalize a list of FileRefs to plain text. Returns (text_parts, warnings)."""
    warnings: list[str] = []
    if not files:
        return [], warnings

    downloaded = await asyncio.gather(
        *[_download_file(f.url) for f in files], return_exceptions=True
    )

    text_parts: list[str] = []
    for file_ref, result in zip(files, downloaded):
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
            text_parts.append(text)

    return text_parts, warnings


async def _download_file(url: str) -> bytes:
    """Download a file from a presigned URL."""
    async with httpx.AsyncClient(timeout=60) as client:
        resp = await client.get(url)
        resp.raise_for_status()
        return resp.content


def _empty_suggestion(task: EvalTask) -> QuestionSuggestion:
    """Create a zero-score suggestion for a blank/not-found answer."""
    flag_note = "not_found" if "not_found" in task.flags else "blank"
    return QuestionSuggestion(
        questionNumber=task.question_number,
        angles={
            "correctness": AngleScore(s=0, note="Không có câu trả lời"),
            "completeness": AngleScore(s=0, note="Không có câu trả lời"),
            "relevance": AngleScore(s=0, note="Không có câu trả lời"),
            "clarity": AngleScore(s=0, note="Không có câu trả lời"),
        },
        score=0.0,
        rationale=f"Học sinh không trả lời câu {task.parent_question} ({flag_note}).",
        evidence=[],
        confidence=1.0,
        flags=list(dict.fromkeys(task.flags + ["manual_only"])),
    )


def _manual_only_suggestion(task: EvalTask, item: ScoreGridItem | None) -> QuestionSuggestion:
    """Non-contract leaves cannot be LLM-scored — flag for manual grading (spec R2)."""
    return QuestionSuggestion(
        questionNumber=task.question_number,
        angles={
            "correctness": AngleScore(s=0, note="Chưa có hợp đồng chấm"),
            "completeness": AngleScore(s=0, note="Chưa có hợp đồng chấm"),
            "relevance": AngleScore(s=0, note="Chưa có hợp đồng chấm"),
            "clarity": AngleScore(s=0, note="Chưa có hợp đồng chấm"),
        },
        score=0.0,
        rationale=(
            "Tiêu chí này chưa có check-items trong hợp đồng chấm đã duyệt — "
            "vui lòng chấm thủ công."
        ),
        evidence=[],
        confidence=0.0,
        flags=["manual_only", "no_contract"],
    )
