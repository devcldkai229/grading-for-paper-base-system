"""Health check endpoints."""

from fastapi import APIRouter

from app.infra.redis_cache import redis_healthy

router = APIRouter(tags=["health"])


@router.get("/health/live")
async def live() -> dict[str, str]:
    return {"status": "ok"}


@router.get("/health/ready")
async def ready() -> dict[str, str]:
    redis_ok = await redis_healthy()
    status = "ready" if redis_ok else "degraded"
    return {"status": status, "redis": "ok" if redis_ok else "unavailable"}
