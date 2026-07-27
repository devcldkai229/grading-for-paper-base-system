# syntax=docker/dockerfile:1
# Shared multi-stage Dockerfile for every .NET service in the monorepo.
# Build context = core/src. Pass PROJECT_PATH + APP_DLL via --build-arg.
ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
ARG PROJECT_PATH
WORKDIR /src

COPY . .

RUN dotnet restore "${PROJECT_PATH}"
RUN dotnet publish "${PROJECT_PATH}" \
      --configuration Release \
      --no-restore \
      --output /app/publish \
      /p:UseAppHost=false \
      /p:DebugType=None \
      /p:DebugSymbols=false

# alpine-extra includes ICU (cultures); krb5-libs needed by Npgsql GSSAPI probing.
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-alpine-extra AS final
ARG APP_DLL
USER root
RUN apk add --no-cache krb5-libs \
    && rm -rf /var/cache/apk/*
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV APP_DLL=${APP_DLL}
EXPOSE 8080

USER $APP_UID

ENTRYPOINT dotnet ${APP_DLL}
