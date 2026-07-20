from pydantic import BaseModel, Field


class ExtractRubricRequest(BaseModel):
    presigned_url: str = Field(alias="presignedUrl")
    content_type: str = Field(alias="contentType")
    subject_max_score: float | None = Field(default=None, alias="subjectMaxScore")

    model_config = {"populate_by_name": True}


class ExtractedQuestion(BaseModel):
    group_label: str | None = Field(default=None, alias="groupLabel")
    question_number: str = Field(alias="questionNumber")
    label: str | None = None
    max_score: float = Field(alias="maxScore")
    confidence: float = 0.0

    model_config = {"populate_by_name": True}


class ExtractRubricResponse(BaseModel):
    questions: list[ExtractedQuestion]
    total_max: float = Field(alias="totalMax")
    warnings: list[str] = []

    model_config = {"populate_by_name": True}
