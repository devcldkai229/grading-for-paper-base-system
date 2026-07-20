from contextlib import asynccontextmanager

from fastapi import FastAPI

from app.infra.openai_client import close_openai, init_openai
from app.infra.redis_cache import close_redis, init_redis
from app.routers import grading, health


@asynccontextmanager
async def lifespan(application: FastAPI):
    """Startup: initialise shared clients; Shutdown: clean up."""
    init_openai()
    await init_redis()
    yield
    await close_redis()
    close_openai()


app = FastAPI(
    title="GradePaper AIGradingService",
    version="0.1.0",
    lifespan=lifespan,
)

app.include_router(health.router)
app.include_router(grading.router)
