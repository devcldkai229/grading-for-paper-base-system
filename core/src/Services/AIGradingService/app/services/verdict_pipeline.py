"""Verdict-based grading path (doc 5.2-5.5 — steps B3→B6).

For leaves that carry a compiled contract (`ScoreGridItem.has_contract`), grading runs check-item
verdicts instead of the free-form angle grader:

    comprehension (B3) → N verdict samples (B4) → adjudication (B5) → CODE scores the tier (B6)

The LLM only judges yes/partial/no/unclear per check-item; the number is computed deterministically
by `scoring.compute_score`, so the score is auditable and cannot drift from the barem.
"""

from __future__ import annotations

import asyncio
import logging

import httpx

from app.config import settings
from app.schemas.grading import AngleScore, QuestionSuggestion, ScoreGridItem
from app.services.adjudicator import adjudicate
from app.services.checkers import apply_deterministic_checkers
from app.services.comprehension import extract_claims
from app.services.confidence import (
    ConfidenceSignals,
    compute_confidence,
    specificity_to_score,
)
from app.services.grader import grade_criterion_samples
from app.services.scoring import ScoreResult, compute_score

logger = logging.getLogger(__name__)


def _norm(text: str) -> str:
    return " ".join(text.lower().split())


def _grounding_fraction(evidence: list[str], answer_text: str) -> float:
    """Fraction of evidence quotes found verbatim (whitespace-normalized) in the answer."""
    if not evidence:
        return 1.0 if not answer_text.strip() else 0.0
    answer_norm = _norm(answer_text)
    grounded = sum(1 for ev in evidence if _norm(ev) and _norm(ev) in answer_norm)
    return grounded / len(evidence)


async def _download_image(url: str) -> bytes | None:
    try:
        async with httpx.AsyncClient(timeout=60) as client:
            resp = await client.get(url)
            resp.raise_for_status()
            return resp.content
    except Exception as exc:  # noqa: BLE001 — a missing image degrades to text-only grading
        logger.warning("Failed to download image %s: %s", url, exc)
        return None


async def load_images_from_urls(urls: list[str]) -> list[bytes]:
    if not urls:
        return []
    downloaded = await asyncio.gather(*[_download_image(u) for u in urls])
    return [d for d in downloaded if d]


def _build_angles(result: ScoreResult, item: ScoreGridItem) -> dict[str, AngleScore]:
    """Synthesize angle scores from the verdict result for UI compatibility."""
    total_checks = len(item.check_items) or 1
    satisfied_ratio = len(result.satisfied) / total_checks
    partial_ratio = len(result.partial) / total_checks
    quality = min(1.0, satisfied_ratio + 0.5 * partial_ratio)
    note = f"{len(result.satisfied)}/{total_checks} tiêu chí đạt"
    return {
        "correctness": AngleScore(s=quality, note=note),
        "completeness": AngleScore(s=satisfied_ratio, note=note),
        "relevance": AngleScore(s=quality, note=note),
        "clarity": AngleScore(s=quality, note=note),
    }


def _build_rationale(result: ScoreResult, item: ScoreGridItem, agreement: float) -> str:
    labels = {c.check_id: (c.description or c.check_id) for c in item.check_items}

    def _names(ids: list[str]) -> str:
        return "; ".join(labels.get(i, i) for i in ids)

    parts = [f"Đề xuất {result.score:.2f}/{item.max_score:.2f} điểm ({result.tier_label})."]
    if result.satisfied:
        parts.append(f"Đạt: {_names(result.satisfied)}.")
    if result.partial:
        parts.append(f"Đạt một phần: {_names(result.partial)}.")
    if result.missing_required:
        parts.append(f"Chưa đủ (bắt buộc): {_names(result.missing_required)}.")
    return " ".join(parts)


async def _leaf_images(item: ScoreGridItem, student_pages: list[bytes]) -> list[bytes]:
    """Barem visual assets + student page images for multimodal verdict (spec B7.2)."""
    if not item.requires_visual and not item.visual_asset_urls:
        return []

    images: list[bytes] = []
    if item.visual_asset_urls:
        images.extend(await load_images_from_urls(item.visual_asset_urls))
    if item.requires_visual:
        images.extend(student_pages)
    return images


async def grade_contract_leaf(
    item: ScoreGridItem,
    answer_text: str,
    student_pages: list[bytes],
    samples: int,
    seg_confidence: float = 1.0,
) -> QuestionSuggestion:
    """Grade one contract leaf through the verdict path."""
    check_ids = [c.check_id for c in item.check_items]

    claims = await extract_claims(item.question_text or item.label or "", answer_text)

    leaf_images = await _leaf_images(item, student_pages)
    verdict_samples = await grade_criterion_samples(
        item, answer_text, claims=claims, images=leaf_images, samples=samples,
    )

    adj = adjudicate(verdict_samples, check_ids)
    verdict_by_check = apply_deterministic_checkers(item, answer_text, adj.verdict_by_check)

    result = compute_score(item, verdict_by_check)

    evidence: list[str] = []
    for evs in adj.evidence_by_check.values():
        evidence.extend(evs)
    evidence = list(dict.fromkeys(evidence))

    vision_unmet = (item.requires_visual or bool(item.visual_asset_urls)) and not leaf_images

    tier_match = 0.6 if (result.missing_required and result.score > 0) else 1.0

    confidence = compute_confidence(ConfidenceSignals(
        agreement=adj.agreement,
        grounding=_grounding_fraction(evidence, answer_text),
        tier_match=tier_match,
        segmentation_quality=seg_confidence,
        extraction_confidence=specificity_to_score(
            item.extraction_confidence or item.specificity
        ),
        vision_unmet=vision_unmet,
    ))

    flags: list[str] = []
    if adj.needs_manual:
        flags.append("manual_only")
    if vision_unmet:
        flags.append("needs_vision")

    return QuestionSuggestion(
        questionNumber=item.question_number,
        angles=_build_angles(result, item),
        score=result.score,
        rationale=_build_rationale(result, item, adj.agreement),
        evidence=evidence,
        confidence=confidence,
        flags=flags,
    )


async def grade_contract_leaves(
    items: list[ScoreGridItem],
    leaf_answers: dict[str, str],
    student_pages: list[bytes],
    seg_confidence: dict[str, float] | None = None,
    samples: int | None = None,
) -> list[QuestionSuggestion]:
    """Grade all contract leaves concurrently. Non-contract leaves are handled by the caller."""
    if not items:
        return []

    samples = samples or settings.verdict_samples
    seg_confidence = seg_confidence or {}

    results = await asyncio.gather(*[
        grade_contract_leaf(
            item,
            leaf_answers.get(item.question_number, ""),
            student_pages,
            samples,
            seg_confidence.get(item.question_number, 1.0),
        )
        for item in items
    ])
    logger.info("Verdict path graded %d contract leaves (samples=%d)", len(items), samples)
    return list(results)
