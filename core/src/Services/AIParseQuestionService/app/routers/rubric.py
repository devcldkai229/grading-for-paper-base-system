from fastapi import APIRouter, Header, HTTPException

from app.config import settings
from app.schemas.rubric import ExtractRubricRequest, ExtractRubricResponse
from app.services.rubric_extractor import extract_rubric_grid

router = APIRouter(prefix="/ai/rubric", tags=["rubric"])


@router.post("/extract", response_model=ExtractRubricResponse)
async def extract_rubric(
    body: ExtractRubricRequest,
    x_internal_api_key: str | None = Header(default=None, alias="X-Internal-Api-Key"),
) -> ExtractRubricResponse:
    if not x_internal_api_key or x_internal_api_key != settings.internal_api_key:
        raise HTTPException(status_code=401, detail="Invalid internal API key")

    try:
        return await extract_rubric_grid(
            body.presigned_url, body.content_type, body.subject_max_score
        )
    except ValueError as exc:
        raise HTTPException(status_code=422, detail=str(exc)) from exc
    except RuntimeError as exc:
        raise HTTPException(status_code=503, detail=str(exc)) from exc
    except Exception as exc:
        raise HTTPException(status_code=500, detail=f"Extraction failed: {exc}") from exc
