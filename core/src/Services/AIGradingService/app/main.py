import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI

from app.config import settings
from app.infra.openai_client import close_openai, init_openai
from app.infra.rabbitmq_consumer import start_consumer_task, stop_consumer
from app.infra.redis_cache import close_redis, init_redis
from app.infra.telemetry import configure_telemetry
from app.routers import grading, health, ingestion

logger = logging.getLogger(__name__)


@asynccontextmanager
async def lifespan(application: FastAPI):
    """Startup: initialise shared clients + AI grading worker; Shutdown: clean up."""
    init_openai()
    await init_redis()
    if settings.rabbitmq_consumer_enabled:
        start_consumer_task()
    else:
        logger.info("RabbitMQ consumer disabled (rabbitmq_consumer_enabled=false)")
    yield
    await stop_consumer()
    await close_redis()
    close_openai()


app = FastAPI(
    title="GradePaper AIGradingService",
    version="0.1.0",
    lifespan=lifespan,
)

configure_telemetry(app)

app.include_router(health.router)
app.include_router(grading.router)
app.include_router(ingestion.router)


if __name__ == "__main__":
    import uvicorn

    uvicorn.run("app.main:app", host="0.0.0.0", port=8081, reload=True)
