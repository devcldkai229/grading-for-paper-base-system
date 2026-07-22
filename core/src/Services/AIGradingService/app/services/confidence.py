"""Composite confidence (doc section 6).

Real confidence is a weighted blend of independent quality signals — not the model's self-reported
number, which is poorly calibrated. The weights below are the design defaults; recalibrate them (and
the accept threshold) against the golden-set harness (`eval/run_golden.py`) whenever the pipeline or
model changes.

    confidence = 0.35·agreement + 0.25·grounding + 0.15·tier_match
               + 0.15·segmentation_quality + 0.10·extraction_confidence      (× vision penalty)
"""

from __future__ import annotations

from dataclasses import dataclass

from app.config import settings

_W_AGREEMENT = 0.35
_W_GROUNDING = 0.25
_W_TIER_MATCH = 0.15
_W_SEGMENTATION = 0.15
_W_EXTRACTION = 0.10


@dataclass
class ConfidenceSignals:
    agreement: float = 1.0              # inter-sample verdict agreement (B5)
    grounding: float = 1.0             # fraction of evidence found verbatim in the answer
    tier_match: float = 1.0           # verdicts map cleanly to a partial-credit tier
    segmentation_quality: float = 1.0  # segmenter boundary confidence for this leaf's parent
    extraction_confidence: float = 1.0  # compiled-contract specificity / extraction confidence
    vision_unmet: bool = False        # visual criterion graded without the required images


def specificity_to_score(value: str | None) -> float:
    """Map a compiled-contract specificity / extraction_confidence label to a 0-1 score."""
    return {"high": 1.0, "medium": 0.7, "low": 0.4}.get((value or "").lower(), 0.7)


def compute_confidence(sig: ConfidenceSignals) -> float:
    """Blend quality signals into a single calibrated confidence in [0, 1]."""
    score = (
        _W_AGREEMENT * sig.agreement
        + _W_GROUNDING * sig.grounding
        + _W_TIER_MATCH * sig.tier_match
        + _W_SEGMENTATION * sig.segmentation_quality
        + _W_EXTRACTION * sig.extraction_confidence
    )
    if sig.vision_unmet:
        score *= settings.vision_confidence_penalty
    return round(max(0.0, min(1.0, score)), 3)
