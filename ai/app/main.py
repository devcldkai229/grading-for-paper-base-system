from fastapi import FastAPI

from app.routers import health, rubric

app = FastAPI(title="GradePaper AIGradingService", version="0.1.0")
app.include_router(health.router)
app.include_router(rubric.router)
