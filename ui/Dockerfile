# syntax=docker/dockerfile:1
# ──────────────────────────────────────────────────────────────────────────────
# Frontend Vite/React -> build tĩnh -> phục vụ bằng nginx.
# Build context = thư mục ui/
# ──────────────────────────────────────────────────────────────────────────────
ARG NODE_VERSION=20

# ── Stage 1: build ────────────────────────────────────────────────────────────
FROM node:${NODE_VERSION}-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build          # tsc -b && vite build  ->  /app/dist

# ── Stage 2: serve ────────────────────────────────────────────────────────────
FROM nginx:1.27-alpine AS final
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
HEALTHCHECK --interval=30s --timeout=3s CMD wget -qO- http://localhost/ >/dev/null 2>&1 || exit 1
