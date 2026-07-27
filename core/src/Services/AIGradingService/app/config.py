from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", extra="ignore")

    # Auth
    internal_api_key: str = ""

    # OpenAI
    openai_api_key: str = ""
    openai_model_t1: str = "gpt-4o-mini"
    openai_model_t2: str = "gpt-4o"
    llm_timeout_seconds: int = 120

    # Confidence & scoring
    confidence_threshold: float = 0.6
    vision_confidence_penalty: float = 0.85
    # Verdict grading (Phase 3): number of independent self-consistency samples per criterion.
    verdict_samples: int = 3

    # Vision / rendering (Phase 2)
    # Gotenberg converts docx -> pdf (same instance ExamCatalog uses for previews).
    gotenberg_url: str = "http://localhost:3000"
    # DPI for rasterising rubric/answer pages into images for the vision model.
    render_dpi: int = 300
    # Cap page images per document to bound token cost.
    max_render_pages: int = 20
    # Strongest available vision model — used for ingestion (A2) and verdicts (B4).
    openai_model_vision: str = "gpt-4o"

    # Redis
    redis_url: str = "redis://:rootpassword@localhost:6380/1"

    # RabbitMQ (event backbone — consume AiGradeRequested, publish AiGradeCompleted/Failed)
    rabbitmq_host: str = "localhost"
    rabbitmq_port: int = 5673
    rabbitmq_username: str = "root"
    rabbitmq_password: str = "rootpassword"
    rabbitmq_vhost: str = "/"
    # Set to false to disable the background consumer (e.g. when running HTTP-only for local tests).
    rabbitmq_consumer_enabled: bool = True

    # Service
    port: int = 8081
    max_concurrent_llm: int = 10
    llm_semaphore_wait_seconds: int = 120
    cache_ttl_seconds: int = 604800  # 7 days

    # Prompt versioning
    prompt_version: str = "v1.0.0"


settings = Settings()
