"""AsyncOpenAI singleton with retry, timeout, and distributed concurrency control."""

from __future__ import annotations

import logging

from openai import AsyncOpenAI

from app.config import settings

logger = logging.getLogger(__name__)

_client: AsyncOpenAI | None = None


def init_openai() -> None:
    """Create the shared AsyncOpenAI client. Called once at startup."""
    global _client

    if not settings.openai_api_key:
        logger.warning("OPENAI_API_KEY not set — LLM calls will fail")

    _client = AsyncOpenAI(
        api_key=settings.openai_api_key,
        timeout=settings.llm_timeout_seconds,
        max_retries=3,
    )
    logger.info(
        "OpenAI client initialised (T1=%s, T2=%s, global_concurrency=%d)",
        settings.openai_model_t1,
        settings.openai_model_t2,
        settings.max_concurrent_llm,
    )


def get_openai() -> AsyncOpenAI:
    """Return the shared client. Raises if not initialised."""
    if _client is None:
        raise RuntimeError("OpenAI client not initialised — call init_openai() first")
    return _client


def close_openai() -> None:
    """Cleanup (no-op for AsyncOpenAI, but keeps lifespan symmetric)."""
    global _client
    _client = None
    logger.info("OpenAI client closed")
