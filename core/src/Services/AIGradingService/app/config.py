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

    # Redis
    redis_url: str = "redis://:rootpassword@localhost:6380/1"

    # GradingService write-back
    grading_service_base_url: str = "http://localhost:5058"

    # Service
    port: int = 8081
    max_concurrent_llm: int = 10
    llm_semaphore_wait_seconds: int = 120
    cache_ttl_seconds: int = 604800  # 7 days

    # Prompt versioning
    prompt_version: str = "v1.0.0"


settings = Settings()
