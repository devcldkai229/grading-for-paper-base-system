"""Pydantic models for grading request/response and OpenAI structured output schema."""

from __future__ import annotations

from enum import Enum
from typing import Any

from pydantic import BaseModel, Field


# ---------------------------------------------------------------------------
# Request models (from GradingService → AIGradingService)
# ---------------------------------------------------------------------------

class FileRef(BaseModel):
    """Reference to a student submission file."""
    url: str = Field(description="Pre-signed URL to download the file")
    content_type: str = Field(alias="contentType", description="MIME type (text/plain, etc.)")

    model_config = {"populate_by_name": True}


class ScoreGridItem(BaseModel):
    """One leaf of the scoring rubric grid."""
    question_number: str = Field(alias="questionNumber")
    group_label: str | None = Field(default=None, alias="groupLabel")
    label: str | None = None
    max_score: float = Field(alias="maxScore")
    parent_question: str | None = Field(
        default=None,
        alias="parentQuestion",
        description="Parent question number (e.g. '1' for leaf '1.1'). Derived if omitted.",
    )
    scoring_rule: str | None = Field(default=None, alias="scoringRule")
    answer_key: str | None = Field(default=None, alias="answerKey")

    model_config = {"populate_by_name": True}


class GradeMode(str, Enum):
    SYNC = "sync"
    BATCH = "batch"


class GradePaperRequest(BaseModel):
    """Request to grade a single student paper."""
    assignment_id: str = Field(alias="assignmentId")
    subject_id: str = Field(alias="subjectId")
    rubric_version: str = Field(default="1", alias="rubricVersion")
    files: list[FileRef]
    score_grid: list[ScoreGridItem] = Field(alias="scoreGrid")
    rubric_text: str | None = Field(
        default=None,
        alias="rubricText",
        description="Full rubric/barem text for Slice 1 (optional).",
    )
    mode: GradeMode = GradeMode.SYNC

    model_config = {"populate_by_name": True}


# ---------------------------------------------------------------------------
# Structured output models (OpenAI JSON schema strict=true)
# ---------------------------------------------------------------------------

class AngleScore(BaseModel):
    """Score for a single evaluation angle (0..1 scale)."""
    s: float = Field(ge=0, le=1, description="Score 0-1 for this angle")
    note: str = Field(description="Brief note explaining the score")


class QuestionSuggestion(BaseModel):
    """AI suggestion for one leaf criterion."""
    question_number: str = Field(alias="questionNumber")
    angles: dict[str, AngleScore] = Field(
        description="Multi-angle scores: correctness, completeness, relevance, clarity"
    )
    score: float = Field(ge=0, description="Proposed score according to scoring rule")
    rationale: str = Field(description="Explanation tied to scoring rule and evidence")
    evidence: list[str] = Field(description="Direct quotes from student answer")
    confidence: float = Field(ge=0, le=1, description="Model confidence 0-1")
    flags: list[str] = Field(
        default_factory=list,
        description="Flags: needs_vision, possible_injection, off_topic, manual_only, missing",
    )

    model_config = {"populate_by_name": True}


class GradingResultLLM(BaseModel):
    """Top-level structured output from LLM (array of suggestions)."""
    suggestions: list[QuestionSuggestion]


# ---------------------------------------------------------------------------
# Response models (AIGradingService → GradingService)
# ---------------------------------------------------------------------------

class GradePaperResponse(BaseModel):
    """Response from the grading endpoint."""
    assignment_id: str = Field(alias="assignmentId")
    suggestions: list[QuestionSuggestion]
    warnings: list[str] = Field(default_factory=list)
    model_used: str = Field(alias="modelUsed")
    prompt_version: str = Field(alias="promptVersion")

    model_config = {"populate_by_name": True}


# ---------------------------------------------------------------------------
# OpenAI Structured Output JSON Schema (strict=true)
# ---------------------------------------------------------------------------

GRADING_JSON_SCHEMA: dict[str, Any] = {
    "name": "grading_result",
    "strict": True,
    "schema": {
        "type": "object",
        "properties": {
            "suggestions": {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "questionNumber": {"type": "string"},
                        "angles": {
                            "type": "object",
                            "properties": {
                                "correctness": {
                                    "type": "object",
                                    "properties": {
                                        "s": {"type": "number"},
                                        "note": {"type": "string"},
                                    },
                                    "required": ["s", "note"],
                                    "additionalProperties": False,
                                },
                                "completeness": {
                                    "type": "object",
                                    "properties": {
                                        "s": {"type": "number"},
                                        "note": {"type": "string"},
                                    },
                                    "required": ["s", "note"],
                                    "additionalProperties": False,
                                },
                                "relevance": {
                                    "type": "object",
                                    "properties": {
                                        "s": {"type": "number"},
                                        "note": {"type": "string"},
                                    },
                                    "required": ["s", "note"],
                                    "additionalProperties": False,
                                },
                                "clarity": {
                                    "type": "object",
                                    "properties": {
                                        "s": {"type": "number"},
                                        "note": {"type": "string"},
                                    },
                                    "required": ["s", "note"],
                                    "additionalProperties": False,
                                },
                            },
                            "required": ["correctness", "completeness", "relevance", "clarity"],
                            "additionalProperties": False,
                        },
                        "score": {"type": "number"},
                        "rationale": {"type": "string"},
                        "evidence": {
                            "type": "array",
                            "items": {"type": "string"},
                        },
                        "confidence": {"type": "number"},
                        "flags": {
                            "type": "array",
                            "items": {"type": "string"},
                        },
                    },
                    "required": [
                        "questionNumber",
                        "angles",
                        "score",
                        "rationale",
                        "evidence",
                        "confidence",
                        "flags",
                    ],
                    "additionalProperties": False,
                },
            },
        },
        "required": ["suggestions"],
        "additionalProperties": False,
    },
}
