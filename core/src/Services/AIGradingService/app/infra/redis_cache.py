"""Async Redis connection pool and generic cache helpers."""

from __future__ import annotations

import json
import logging
from typing import Any

import redis.asyncio as aioredis

from app.config import settings

logger = logging.getLogger(__name__)

_pool: aioredis.Redis | None = None


async def init_redis() -> None:
    """Create the shared Redis connection pool. Called once at startup."""
    global _pool
    try:
        _pool = aioredis.from_url(
            settings.redis_url,
            decode_responses=True,
            socket_connect_timeout=5,
        )
        # Verify connectivity
        await _pool.ping()
        logger.info("Redis connected: %s", settings.redis_url)
    except Exception:
        logger.warning("Redis unavailable — caching disabled", exc_info=True)
        _pool = None


def get_redis() -> aioredis.Redis | None:
    """Return the shared Redis client, or None if unavailable."""
    return _pool


async def close_redis() -> None:
    """Close the Redis connection pool."""
    global _pool
    if _pool is not None:
        await _pool.aclose()
        _pool = None
        logger.info("Redis connection closed")


# ---------------------------------------------------------------------------
# Generic cache helpers (graceful degradation — cache miss on Redis failure)
# ---------------------------------------------------------------------------

async def get_cached(key: str) -> Any | None:
    """Retrieve a JSON-serialised value from cache. Returns None on miss or error."""
    pool = get_redis()
    if pool is None:
        return None
    try:
        raw = await pool.get(key)
        if raw is None:
            return None
        return json.loads(raw)
    except Exception:
        logger.debug("Cache get failed for key=%s", key, exc_info=True)
        return None


async def set_cached(key: str, value: Any, ttl: int | None = None) -> None:
    """Store a JSON-serialisable value in cache. Silently ignores errors."""
    pool = get_redis()
    if pool is None:
        return
    try:
        serialised = json.dumps(value, ensure_ascii=False)
        if ttl is None:
            ttl = settings.cache_ttl_seconds
        await pool.set(key, serialised, ex=ttl)
    except Exception:
        logger.debug("Cache set failed for key=%s", key, exc_info=True)


# ---------------------------------------------------------------------------
# Inbox dedupe (SET NX) — protects the expensive LLM call from redelivery
# ---------------------------------------------------------------------------

async def claim_once(key: str, ttl: int = 86400) -> bool:
    """Atomically claim a one-time key via SET NX.

    Returns True if the key was newly claimed (caller should proceed), False if it already
    existed (duplicate — caller should skip). Fails open: when Redis is unavailable we return
    True so a cache outage never blocks grading (reply-side idempotency is the safety net).
    """
    pool = get_redis()
    if pool is None:
        return True
    try:
        acquired = await pool.set(key, "1", nx=True, ex=ttl)
        return bool(acquired)
    except Exception:
        logger.debug("claim_once failed for key=%s", key, exc_info=True)
        return True


async def release_claim(key: str) -> None:
    """Release a previously claimed key so a legitimate retry can re-run. Best-effort."""
    pool = get_redis()
    if pool is None:
        return
    try:
        await pool.delete(key)
    except Exception:
        logger.debug("release_claim failed for key=%s", key, exc_info=True)


async def delete_by_pattern(pattern: str) -> int:
    """Delete all keys matching a glob pattern (SCAN-based; best-effort). Returns count deleted."""
    pool = get_redis()
    if pool is None:
        return 0
    deleted = 0
    try:
        async for key in pool.scan_iter(match=pattern, count=200):
            await pool.delete(key)
            deleted += 1
    except Exception:
        logger.debug("delete_by_pattern failed for pattern=%s", pattern, exc_info=True)
    return deleted


async def redis_healthy() -> bool:
    """Check Redis connectivity for readiness probe."""
    pool = get_redis()
    if pool is None:
        return False
    try:
        return await pool.ping()
    except Exception:
        return False
