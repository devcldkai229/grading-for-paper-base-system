# syntax=docker/dockerfile:1
FROM python:3.12-slim AS deps
WORKDIR /build
COPY requirements.txt .
RUN pip install --no-cache-dir --prefix=/install -r requirements.txt

FROM python:3.12-slim AS final
RUN useradd -m -u 10001 appuser
WORKDIR /app

COPY --from=deps /install /usr/local
COPY app ./app

USER appuser
EXPOSE 8080
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8080"]
