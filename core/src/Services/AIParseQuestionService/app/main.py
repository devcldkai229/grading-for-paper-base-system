from fastapi import FastAPI

from app.routers import health, rubric

app = FastAPI(title="GradePaper AIParseQuestionService", version="0.1.0")
app.include_router(health.router)
app.include_router(rubric.router)


if __name__ == "__main__":
    import uvicorn

    uvicorn.run("app.main:app", host="0.0.0.0", port=8080, reload=True)
