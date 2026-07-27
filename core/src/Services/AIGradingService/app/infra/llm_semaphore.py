"""Redis-based distributed semaphore for global LLM concurrency control."""

from __future__ import annotations

import asyncio
import logging
import time
import uuid
from contextlib import asynccontextmanager
from typing import AsyncIterator

from app.config import settings
from app.infra.redis_cache import get_redis

logger = logging.getLogger(__name__)

_LLM_ACTIVE_KEY = "llm:concurrent:active"
_LLM_SLOT_PREFIX = "llm:slot:"


@asynccontextmanager
async def llm_slot() -> AsyncIterator[None]:
    """Acquire a global LLM slot (Redis) with local asyncio fallback.

    Waits up to ``llm_semaphore_wait_seconds`` then raises TimeoutError.
    """
    redis = get_redis()
    token = uuid.uuid4().hex
    acquired = False
    local_sem = _get_local_semaphore()

    if redis is None:
        async with local_sem:
            yield
        return

    deadline = time.monotonic() + settings.llm_semaphore_wait_seconds
    while time.monotonic() < deadline:
        try:
            current = await redis.incr(_LLM_ACTIVE_KEY)
            if current <= settings.max_concurrent_llm:
                await redis.set(f"{_LLM_SLOT_PREFIX}{token}", "1", ex=settings.llm_timeout_seconds + 60)
                acquired = True
                break
            await redis.decr(_LLM_ACTIVE_KEY)
        except Exception:
            logger.debug("Redis LLM semaphore unavailable — using local semaphore", exc_info=True)
            async with local_sem:
                yield
            return

        await asyncio.sleep(0.25)

    if not acquired:
        raise TimeoutError(
            f"LLM concurrency limit ({settings.max_concurrent_llm}) reached — timed out waiting for slot"
        )

    try:
        yield
    finally:
        try:
            await redis.delete(f"{_LLM_SLOT_PREFIX}{token}")
            remaining = await redis.decr(_LLM_ACTIVE_KEY)
            if remaining < 0:
                await redis.set(_LLM_ACTIVE_KEY, 0)
        except Exception:
            logger.debug("Failed to release Redis LLM slot", exc_info=True)


_local_semaphore: asyncio.Semaphore | None = None


def _get_local_semaphore() -> asyncio.Semaphore:
    global _local_semaphore
    if _local_semaphore is None:
        _local_semaphore = asyncio.Semaphore(settings.max_concurrent_llm)
    return _local_semaphore
