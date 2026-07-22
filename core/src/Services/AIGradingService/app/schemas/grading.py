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


class ContractCheckItem(BaseModel):
    """An atomic, verifiable requirement compiled from the barem (Phase 3)."""
    check_id: str = Field(alias="checkId")
    description: str = ""
    points: float = 0.0
    required: bool = False
    evidence_hint: str | None = Field(default=None, alias="evidenceHint")

    model_config = {"populate_by_name": True}


class ContractPartialCredit(BaseModel):
    """A score band mapped to satisfied check-items."""
    label: str = ""
    score: float = 0.0
    condition: str = ""
    check_ids: list[str] = Field(default_factory=list, alias="checkIds")

    model_config = {"populate_by_name": True}


class ScoreGridItem(BaseModel):
    """One leaf of the scoring rubric grid, optionally enriched by a compiled contract."""
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

    # ── Compiled-contract enrichment (Phase 3) — empty when no approved contract ──
    question_text: str | None = Field(default=None, alias="questionText")
    check_items: list[ContractCheckItem] = Field(default_factory=list, alias="checkItems")
    partial_credit: list[ContractPartialCredit] = Field(default_factory=list, alias="partialCredit")
    common_mistakes: list[str] = Field(default_factory=list, alias="commonMistakes")
    not_penalize: list[str] = Field(default_factory=list, alias="notPenalize")
    requires_visual: bool = Field(default=False, alias="requiresVisual")
    visual_asset_urls: list[str] = Field(default_factory=list, alias="visualAssetUrls")
    specificity: str | None = None
    extraction_confidence: str | None = Field(default=None, alias="extractionConfidence")

    model_config = {"populate_by_name": True}

    @property
    def has_contract(self) -> bool:
        """True when this leaf carries compiled check-items (use the verdict path)."""
        return len(self.check_items) > 0


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
    rubric_files: list[FileRef] = Field(
        default_factory=list,
        alias="rubricFiles",
        description="Original barem file(s) (+ optional exam paper) — downloaded and normalized into "
        "the rubric context so the grader reads the real questions/keys/guide.",
    )
    compiled_rubric_version: str | None = Field(default=None, alias="compiledRubricVersion")
    language: str = "vi"
    student_page_image_urls: list[str] = Field(
        default_factory=list, alias="studentPageImageUrls",
        description="Presigned page images of the student paper for multimodal grading.",
    )
    mode: GradeMode = GradeMode.SYNC

    model_config = {"populate_by_name": True}


# ---------------------------------------------------------------------------
# Structured output models (OpenAI JSON schema strict=true)
# ---------------------------------------------------------------------------

class AngleScore(BaseModel):
    """Score for a single evaluation angle (0..1 scale)."""
    s: float = Field(ge=0, le=1, description="Điểm 0-1 cho góc đánh giá này")
    note: str = Field(description="Ghi chú ngắn bằng TIẾNG VIỆT giải thích điểm số")


class QuestionSuggestion(BaseModel):
    """AI suggestion for one leaf criterion."""
    question_number: str = Field(alias="questionNumber")
    angles: dict[str, AngleScore] = Field(
        description="Điểm đa góc: correctness, completeness, relevance, clarity"
    )
    score: float = Field(ge=0, description="Điểm đề xuất theo quy tắc chấm (scoringRule)")
    rationale: str = Field(
        description="Giải thích bằng TIẾNG VIỆT, bám sát quy tắc chấm và dẫn chứng"
    )
    evidence: list[str] = Field(
        description="Trích dẫn NGUYÊN VĂN từ bài làm học sinh (giữ nguyên ngôn ngữ gốc, không dịch)"
    )
    confidence: float = Field(ge=0, le=1, description="Mức độ tự tin của mô hình 0-1")
    flags: list[str] = Field(
        default_factory=list,
        description="Flags: needs_vision, possible_injection, off_topic, manual_only, "
        "blank, not_found, coverage_miss",
    )

    model_config = {"populate_by_name": True}


class GradingResultLLM(BaseModel):
    """Top-level structured output from LLM (array of suggestions)."""
    suggestions: list[QuestionSuggestion]


# ---------------------------------------------------------------------------
# Verdict-based grading (Phase 3 — one call per criterion, verdict per check-item)
# ---------------------------------------------------------------------------

class CheckVerdict(BaseModel):
    """LLM verdict for a single check-item (no score — code computes the score)."""
    check_id: str = Field(alias="checkId")
    verdict: str = Field(description="yes | partial | no | unclear")
    evidence: list[str] = Field(default_factory=list, description="Verbatim quotes from the answer")
    note: str = ""

    model_config = {"populate_by_name": True}


class CriterionVerdict(BaseModel):
    """One sample's verdicts across all check-items of a criterion."""
    verdicts: list[CheckVerdict] = Field(default_factory=list)
    confidence: float = 0.5


# OpenAI structured-output schema for a single criterion's verdicts.
VERDICT_JSON_SCHEMA: dict[str, Any] = {
    "name": "criterion_verdict",
    "strict": True,
    "schema": {
        "type": "object",
        "properties": {
            "verdicts": {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "checkId": {"type": "string"},
                        "verdict": {"type": "string", "enum": ["yes", "partial", "no", "unclear"]},
                        "evidence": {"type": "array", "items": {"type": "string"}},
                        "note": {"type": "string"},
                    },
                    "required": ["checkId", "verdict", "evidence", "note"],
                    "additionalProperties": False,
                },
            },
            "confidence": {"type": "number"},
        },
        "required": ["verdicts", "confidence"],
        "additionalProperties": False,
    },
}


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
