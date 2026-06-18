-- ============================================================
-- IDENTITY & ACCESS SERVICE  (PostgreSQL)   db: gradepaper_identity
-- Owns: users, authentication, refresh tokens, 2FA.
-- Roles: chỉ 2 vai trò — 'lecturer' (User/giảng viên) và 'admin'.
-- Cross-service: KHÔNG có FK ra ngoài service này.
-- ============================================================
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- ── ENUMS ────────────────────────────────────────────────────
CREATE TYPE login_provider AS ENUM ('password_auth', 'google');
CREATE TYPE user_status    AS ENUM ('active', 'inactive', 'locked');
CREATE TYPE user_role      AS ENUM ('lecturer', 'admin');   -- chỉ 2 role

-- ── USERS ────────────────────────────────────────────────────
CREATE TABLE users (
    id              UUID           PRIMARY KEY DEFAULT gen_random_uuid(),
    email           VARCHAR(255)   NOT NULL UNIQUE,
    password_hash   VARCHAR(255),                       -- NULL nếu chỉ đăng nhập OAuth
    google_id       VARCHAR(128),                       -- OAuth subject (Google)
    avatar_url      VARCHAR(500),                       -- ảnh đại diện từ OAuth
    full_name       VARCHAR(255),
    phone_number    VARCHAR(50),
    -- alias hiển thị của giáo viên trong file điểm (vd: HungLD5, LamNN15)
    marker_code     VARCHAR(50),
    login_provider  login_provider NOT NULL DEFAULT 'password_auth',
    status          user_status    NOT NULL DEFAULT 'active',
    role            user_role      NOT NULL DEFAULT 'lecturer',
    last_login_at   TIMESTAMPTZ,
    created_at      TIMESTAMPTZ    NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ
);

-- ── REFRESH TOKENS (rotating) ────────────────────────────────
CREATE TABLE refresh_tokens (
    id          UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     UUID         NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash  VARCHAR(512) NOT NULL UNIQUE,           -- lưu hash, không lưu token thô
    jwt_id      VARCHAR(64)  NOT NULL,                  -- ghép cặp với access token (rotation)
    user_agent  VARCHAR(300),
    ip_address  VARCHAR(45),                          -- IPv4/IPv6
    expires_at  TIMESTAMPTZ  NOT NULL,
    is_used     BOOLEAN      NOT NULL DEFAULT FALSE,    -- phát hiện reuse khi rotate
    is_revoked  BOOLEAN      NOT NULL DEFAULT FALSE,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

-- ── INDEXES ──────────────────────────────────────────────────
CREATE INDEX idx_users_role               ON users(role);
CREATE INDEX idx_users_status             ON users(status);
CREATE INDEX idx_refresh_tokens_user_id   ON refresh_tokens(user_id);
CREATE INDEX idx_refresh_tokens_token     ON refresh_tokens(token_hash);
CREATE INDEX idx_refresh_tokens_expires   ON refresh_tokens(expires_at) WHERE is_revoked = FALSE;
