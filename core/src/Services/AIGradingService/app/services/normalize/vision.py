"""Vision helpers (doc 4.3) — attach page/asset images to OpenAI multimodal calls.

Keeps the low-level image-encoding + chat-with-images plumbing in one place so ingestion (A2) and
grading verdicts (B4) share exactly the same vision call path.
"""

from __future__ import annotations

import base64
import json
import logging
from typing import Any

from app.config import settings
from app.infra.llm_semaphore import llm_slot
from app.infra.openai_client import get_openai
from app.infra.telemetry import get_tracer

logger = logging.getLogger(__name__)


def image_data_url(image_bytes: bytes, mime: str = "image/png") -> str:
    """Encode raw image bytes as a base64 data URL for the OpenAI vision API."""
    b64 = base64.b64encode(image_bytes).decode("ascii")
    return f"data:{mime};base64,{b64}"


def image_part(image_bytes: bytes, mime: str = "image/png", detail: str = "high") -> dict[str, Any]:
    """Build a single image content part."""
    return {
        "type": "image_url",
        "image_url": {"url": image_data_url(image_bytes, mime), "detail": detail},
    }


def build_content(text: str, images: list[bytes], mime: str = "image/png") -> list[dict[str, Any]]:
    """Build a multimodal user-message content array: leading text + N images."""
    content: list[dict[str, Any]] = [{"type": "text", "text": text}]
    content.extend(image_part(img, mime) for img in images)
    return content


async def vision_json(
    *,
    system: str,
    user_text: str,
    images: list[bytes],
    schema: dict[str, Any],
    model: str | None = None,
    temperature: float = 0.1,
    mime: str = "image/png",
) -> dict[str, Any]:
    """Run a structured-output vision call (text + images -> strict JSON)."""
    model = model or settings.openai_model_vision
    client = get_openai()

    with get_tracer().start_as_current_span("openai.chat.completions.vision") as span:
        span.set_attribute("gen_ai.system", "openai")
        span.set_attribute("gen_ai.request.model", model)
        span.set_attribute("vision.image_count", len(images))

        async with llm_slot():
            completion = await client.chat.completions.create(
                model=model,
                messages=[
                    {"role": "system", "content": system},
                    {"role": "user", "content": build_content(user_text, images, mime)},
                ],
                response_format={"type": "json_schema", "json_schema": schema},
                temperature=temperature,
            )

    raw = completion.choices[0].message.content or "{}"
    try:
        return json.loads(raw)
    except json.JSONDecodeError as exc:
        logger.warning("Vision call returned invalid JSON: %s", exc)
        raise
