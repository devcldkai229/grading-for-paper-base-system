-- ============================================================
-- NOTIFICATION SERVICE  (PostgreSQL)  db: gradepaper_notification
-- Owns: thông báo in-app + email, mẫu thông báo.
-- Tiêu thụ message bất đồng bộ (RabbitMQ): assignment, nhắc deadline,
-- export xong, re-grade... rồi gửi & lưu trạng thái.
-- Cross-service refs (no FK): user_id.
-- ============================================================
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- ── ENUMS ────────────────────────────────────────────────────
CREATE TYPE notification_channel AS ENUM ('in_app', 'email');
CREATE TYPE notification_status  AS ENUM ('pending', 'sent', 'failed', 'read');
CREATE TYPE notification_type    AS ENUM (
    'assignment',       -- được phân công chấm
    'deadline_reminder',-- nhắc deadline
    'regrade_request',  -- yêu cầu phúc khảo
    'export_ready',     -- file điểm đã sẵn sàng tải
    'system'            -- thông báo hệ thống
);

-- ── NOTIFICATION TEMPLATES ───────────────────────────────────
CREATE TABLE notification_templates (
    id         UUID              PRIMARY KEY DEFAULT gen_random_uuid(),
    type       notification_type NOT NULL,
    channel    notification_channel NOT NULL,
    subject    VARCHAR(255),                            -- tiêu đề email
    body_template TEXT           NOT NULL,              -- có placeholder {{name}} ...
    is_active  BOOLEAN           NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ       NOT NULL DEFAULT NOW(),
    UNIQUE (type, channel)
);

-- ── NOTIFICATIONS ────────────────────────────────────────────
CREATE TABLE notifications (
    id          UUID                 PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     UUID                 NOT NULL,          -- cross-service ref
    type        notification_type    NOT NULL,
    channel     notification_channel NOT NULL DEFAULT 'in_app',
    title       VARCHAR(255)         NOT NULL,
    body        TEXT,
    -- link/ngữ cảnh để FE điều hướng (vd: {"subjectId":"...","paperId":"..."})
    metadata    JSONB,
    status      notification_status  NOT NULL DEFAULT 'pending',
    is_read     BOOLEAN              NOT NULL DEFAULT FALSE,
    sent_at     TIMESTAMPTZ,
    read_at     TIMESTAMPTZ,
    created_at  TIMESTAMPTZ          NOT NULL DEFAULT NOW()
);

-- ── INDEXES ──────────────────────────────────────────────────
CREATE INDEX idx_notifications_user    ON notifications(user_id);
CREATE INDEX idx_notifications_unread  ON notifications(user_id) WHERE is_read = FALSE;
CREATE INDEX idx_notifications_status  ON notifications(status);
