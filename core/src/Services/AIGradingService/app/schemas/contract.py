"""Compiled-rubric contract schemas (doc 4.2 / 4.4 / 7.1).

These model the *authoritative* grading contract produced by the ingestion pipeline (Phase 2) and
consumed by the verdict-based grader (Phase 3). A contract is compiled once per subject/rubric
version, reviewed by an admin (Phase 2d), then locked.

Vocabulary:
- Block          : a labelled region of the barem (question / answer_key / grading_guide / illustration)
- CheckItem      : one atomic, verifiable requirement inside a criterion
- PartialCreditTier : a score band mapped to a set of satisfied check-items
- CompiledCriterion : everything needed to grade ONE leaf (question) deterministically
- CompiledContract : the whole rubric = criteria + assets
"""

from __future__ import annotations

from enum import Enum

from pydantic import BaseModel, Field


class BlockRole(str, Enum):
    QUESTION = "question"
    ANSWER_KEY = "answer_key"
    GRADING_GUIDE = "grading_guide"
    ILLUSTRATION = "illustration"
    OTHER = "other"


class BlockSource(str, Enum):
    TEXT = "text"
    VISION = "vision"
    BOTH = "both"


class Confidence(str, Enum):
    HIGH = "high"
    MEDIUM = "medium"
    LOW = "low"


class BBox(BaseModel):
    """Normalised bounding box (0..1) relative to page width/height."""
    x0: float = 0.0
    y0: float = 0.0
    x1: float = 1.0
    y1: float = 1.0


class Block(BaseModel):
    """A labelled region extracted from the barem."""
    block_id: str = Field(alias="blockId")
    role: BlockRole
    question: str | None = Field(default=None, description="Question number this block belongs to")
    text: str = ""
    page_index: int = Field(default=0, alias="pageIndex")
    bbox: BBox | None = None
    source: BlockSource = BlockSource.TEXT
    confidence: Confidence = Confidence.MEDIUM

    model_config = {"populate_by_name": True}


class CheckItem(BaseModel):
    """One atomic, verifiable requirement inside a criterion."""
    check_id: str = Field(alias="checkId")
    description: str = Field(description="What must be true, in Vietnamese")
    points: float = Field(default=0.0, description="Points contributed when satisfied")
    required: bool = Field(default=False, description="Must be satisfied for any credit")
    evidence_hint: str | None = Field(default=None, alias="evidenceHint")

    model_config = {"populate_by_name": True}


class PartialCreditTier(BaseModel):
    """A score band mapped to satisfied check-items."""
    label: str = Field(description="Tier label, e.g. 'Đầy đủ', 'Một phần', 'Không đạt'")
    score: float = Field(description="Score awarded for this tier (absolute points)")
    condition: str = Field(description="When this tier applies, in Vietnamese")
    check_ids: list[str] = Field(
        default_factory=list, alias="checkIds",
        description="Check-item ids that must be satisfied for this tier",
    )

    model_config = {"populate_by_name": True}


class Specificity(str, Enum):
    HIGH = "high"
    MEDIUM = "medium"
    LOW = "low"


class CompiledCriterion(BaseModel):
    """Everything needed to grade ONE leaf criterion deterministically."""
    question_number: str = Field(alias="questionNumber")
    parent_question: str | None = Field(default=None, alias="parentQuestion")
    group_label: str | None = Field(default=None, alias="groupLabel")
    label: str | None = None
    max_score: float = Field(alias="maxScore")

    question_text: str = Field(default="", alias="questionText")
    answer_key: str | None = Field(default=None, alias="answerKey")
    check_items: list[CheckItem] = Field(default_factory=list, alias="checkItems")
    partial_credit: list[PartialCreditTier] = Field(default_factory=list, alias="partialCredit")
    common_mistakes: list[str] = Field(default_factory=list, alias="commonMistakes")
    not_penalize: list[str] = Field(default_factory=list, alias="notPenalize")

    requires_visual: bool = Field(default=False, alias="requiresVisual")
    visual_asset_ids: list[str] = Field(default_factory=list, alias="visualAssetIds")
    source_block_ids: list[str] = Field(default_factory=list, alias="sourceBlockIds")

    specificity: Specificity = Specificity.MEDIUM
    extraction_confidence: Confidence = Field(default=Confidence.MEDIUM, alias="extractionConfidence")

    model_config = {"populate_by_name": True}


class RubricAsset(BaseModel):
    """An illustration/table/formula cropped from the barem, stored for reuse during grading."""
    asset_id: str = Field(alias="assetId")
    kind: str = Field(default="illustration", description="illustration | table | formula")
    question: str | None = None
    page_index: int = Field(default=0, alias="pageIndex")
    bbox: BBox | None = None
    # One of these is populated: base64 crop (from AI ingestion) OR a stored URL/key (after persist).
    image_base64: str | None = Field(default=None, alias="imageBase64")
    mime: str = "image/png"
    s3_key: str | None = Field(default=None, alias="s3Key")
    url: str | None = None

    model_config = {"populate_by_name": True}


class CompiledContract(BaseModel):
    """The authoritative compiled rubric for one subject/rubric version."""
    subject_id: str = Field(alias="subjectId")
    rubric_version: str = Field(default="1", alias="rubricVersion")
    language: str = "vi"
    criteria: list[CompiledCriterion] = Field(default_factory=list)
    assets: list[RubricAsset] = Field(default_factory=list)
    blocks: list[Block] = Field(default_factory=list)
    warnings: list[str] = Field(default_factory=list)
    coverage_ok: bool = Field(default=False, alias="coverageOk")

    model_config = {"populate_by_name": True}


# ---------------------------------------------------------------------------
# Ingestion request/response (ExamCatalog -> AI HTTP)
# ---------------------------------------------------------------------------

class IngestFileRef(BaseModel):
    url: str
    content_type: str = Field(default="application/octet-stream", alias="contentType")
    kind: str = "rubric"  # rubric | exam_paper

    model_config = {"populate_by_name": True}


class IngestRubricRequest(BaseModel):
    subject_id: str = Field(alias="subjectId")
    rubric_version: str = Field(default="1", alias="rubricVersion")
    max_score: float = Field(default=10.0, alias="maxScore")
    files: list[IngestFileRef] = Field(default_factory=list)
    score_grid: list[dict] = Field(default_factory=list, alias="scoreGrid")

    model_config = {"populate_by_name": True}


class IngestRubricResponse(BaseModel):
    contract: CompiledContract
    model_used: str = Field(default="", alias="modelUsed")

    model_config = {"populate_by_name": True}
