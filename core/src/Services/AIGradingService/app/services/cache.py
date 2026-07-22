"""Suggestion Cache — per-question caching keyed on answer content hash.

Key schema:  suggestion:{subject_id}:{rubric_version}:{question_number}:{sha256(answer_text)}
Value:       JSON-serialised QuestionSuggestion

Cache check happens BEFORE LLM call; cache write happens AFTER verification.
This means only verified (guardrail-passed) suggestions are cached.
"""

from __future__ import annotations

import hashlib
import logging

from app.config import settings
from app.infra.redis_cache import delete_by_pattern, get_cached, set_cached
from app.schemas.grading import AngleScore, QuestionSuggestion

logger = logging.getLogger(__name__)

_CACHE_PREFIX = "suggestion"


def _cache_key(
    subject_id: str,
    rubric_version: str,
    question_number: str,
    answer_text: str,
) -> str:
    """Build the cache key for a suggestion."""
    answer_hash = hashlib.sha256(answer_text.encode("utf-8")).hexdigest()[:16]
    return f"{_CACHE_PREFIX}:{subject_id}:{rubric_version}:{question_number}:{answer_hash}"


async def get_cached_suggestion(
    subject_id: str,
    rubric_version: str,
    question_number: str,
    answer_text: str,
) -> QuestionSuggestion | None:
    """Look up a cached suggestion. Returns None on miss."""
    key = _cache_key(subject_id, rubric_version, question_number, answer_text)
    data = await get_cached(key)
    if data is None:
        return None

    try:
        # Reconstruct Pydantic model from cached dict
        angles = {}
        for name, angle_data in data.get("angles", {}).items():
            angles[name] = AngleScore(s=angle_data["s"], note=angle_data["note"])

        suggestion = QuestionSuggestion(
            questionNumber=data["question_number"],
            angles=angles,
            score=data["score"],
            rationale=data["rationale"],
            evidence=data["evidence"],
            confidence=data["confidence"],
            flags=data.get("flags", []),
        )
        logger.debug("Cache hit: Q%s", question_number)
        return suggestion
    except (KeyError, TypeError, ValueError) as exc:
        logger.debug("Cache deserialization failed for Q%s: %s", question_number, exc)
        return None


async def set_cached_suggestion(
    subject_id: str,
    rubric_version: str,
    question_number: str,
    answer_text: str,
    suggestion: QuestionSuggestion,
) -> None:
    """Store a verified suggestion in cache."""
    key = _cache_key(subject_id, rubric_version, question_number, answer_text)

    # Serialise to a plain dict for JSON storage
    data = {
        "question_number": suggestion.question_number,
        "angles": {
            name: {"s": angle.s, "note": angle.note}
            for name, angle in suggestion.angles.items()
        },
        "score": suggestion.score,
        "rationale": suggestion.rationale,
        "evidence": suggestion.evidence,
        "confidence": suggestion.confidence,
        "flags": suggestion.flags,
    }

    await set_cached(key, data, ttl=settings.cache_ttl_seconds)
    logger.debug("Cache set: Q%s (key=%s)", question_number, key)


async def invalidate_suggestions(subject_id: str, rubric_version: str | None = None) -> int:
    """Drop cached suggestions for a subject (optionally a single rubric version).

    Called when a compiled rubric changes/recompiles. Version-scoped keys make a new version
    naturally isolated; this covers same-version recompiles (e.g. admin edits + re-approval).
    """
    version = rubric_version if rubric_version is not None else "*"
    pattern = f"{_CACHE_PREFIX}:{subject_id}:{version}:*"
    deleted = await delete_by_pattern(pattern)
    logger.info("Invalidated %d cached suggestions (pattern=%s)", deleted, pattern)
    return deleted
