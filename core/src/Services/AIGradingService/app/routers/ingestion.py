"""Rubric ingestion API (doc 4 / 7.1).

POST /ai/ingest/rubric — compile a subject's barem into a CompiledContract (blocks + check-items +
partial-credit + assets). Called by ExamCatalog on rubric upload / version change; the returned
contract is persisted by ExamCatalog and reviewed by an admin before it becomes usable for grading.
"""

from __future__ import annotations

import logging

import httpx
from fastapi import APIRouter, Header, HTTPException

from app.config import settings
from app.schemas.contract import IngestRubricRequest, IngestRubricResponse
from app.services.ingestion.compiler import compile_contract
from app.services.normalize.renderer import render_document

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/ai/ingest", tags=["ingestion"])


async def _download(url: str) -> bytes:
    async with httpx.AsyncClient(timeout=120) as client:
        resp = await client.get(url)
        resp.raise_for_status()
        return resp.content


@router.post("/rubric", response_model=IngestRubricResponse)
async def ingest_rubric(
    body: IngestRubricRequest,
    x_internal_api_key: str | None = Header(default=None, alias="X-Internal-Api-Key"),
) -> IngestRubricResponse:
    """Render + extract + compile a subject's barem into a CompiledContract."""
    if not x_internal_api_key or x_internal_api_key != settings.internal_api_key:
        raise HTTPException(status_code=401, detail="Invalid internal API key")

    rubric_files = [f for f in body.files if f.kind == "rubric"] or body.files
    if not rubric_files:
        raise HTTPException(status_code=422, detail="No rubric file provided")

    primary = rubric_files[0]
    try:
        data = await _download(primary.url)
        rendered = await render_document(data, primary.content_type, primary.url)
    except Exception as exc:
        logger.exception("Rubric render failed subject=%s", body.subject_id)
        raise HTTPException(status_code=502, detail=f"Render failed: {exc}") from exc

    try:
        contract = await compile_contract(
            subject_id=body.subject_id,
            rubric_version=body.rubric_version,
            score_grid=body.score_grid,
            rendered=rendered,
        )
    except Exception as exc:
        logger.exception("Rubric compile failed subject=%s", body.subject_id)
        raise HTTPException(status_code=500, detail=f"Compile failed: {exc}") from exc

    return IngestRubricResponse(contract=contract, modelUsed=settings.openai_model_t2)
