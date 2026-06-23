# syntax=docker/dockerfile:1
# ──────────────────────────────────────────────────────────────────────────────
# Dockerfile DÙNG CHUNG cho mọi .NET service trong monorepo.
# Mỗi service build ra 1 image riêng (đúng tinh thần microservices) bằng cách
# truyền PROJECT_PATH + APP_DLL qua --build-arg. Xem .github/workflows/delivery.yml
# Build context = thư mục core/src (chứa Services/, ApiGateway/, BuildingBlocks/).
# ──────────────────────────────────────────────────────────────────────────────
ARG DOTNET_VERSION=10.0

# ── Stage 1: build & publish ──────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
ARG PROJECT_PATH
WORKDIR /src

# Copy toàn bộ source vì các project reference (API→Application→Domain→Contracts)
# nằm rải rác trong core/src.
COPY . .

RUN dotnet restore "${PROJECT_PATH}"
RUN dotnet publish "${PROJECT_PATH}" \
      --configuration Release \
      --no-restore \
      --output /app/publish \
      /p:UseAppHost=false

# ── Stage 2: runtime (nhỏ gọn, chạy non-root) ─────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final
ARG APP_DLL
WORKDIR /app
COPY --from=build /app/publish .

# Lắng nghe 8080 (user non-root không bind được cổng <1024).
# Service đứng SAU API Gateway nên không cần HTTPS ở đây.
ENV ASPNETCORE_URLS=http://+:8080
ENV APP_DLL=${APP_DLL}
EXPOSE 8080

# User non-root có sẵn trong image .NET (>=8)
USER $APP_UID

# shell-form để ${APP_DLL} được expand lúc container chạy
ENTRYPOINT dotnet ${APP_DLL}
