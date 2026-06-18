-- ============================================================
-- REPORTING & ANALYTICS SERVICE  (PostgreSQL)  db: gradepaper_reporting
-- Owns: bảng tổng hợp (read-model) cho dashboard admin + export XLSX.
-- Dữ liệu được làm tươi từ Grading qua gRPC (pull) + message tiến độ (async).
-- KHÔNG nằm trên đường ghi điểm => eventual consistency, ưu tiên availability.
-- Cross-service refs (no FK): subject_id, teacher_id, requested_by.
-- ============================================================
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- ── ENUMS ────────────────────────────────────────────────────
CREATE TYPE export_status AS ENUM ('queued', 'processing', 'completed', 'failed');

-- ── EXPORT JOBS (xuất file điểm Mark_Input.xlsx — Hangfire) ──
CREATE TABLE export_jobs (
    id             UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
    subject_id     UUID          NOT NULL,              -- cross-service ref
    requested_by   UUID          NOT NULL,              -- user_id cross-service ref
    result_s3_key  TEXT,                                -- S3: file Mark_Input.xlsx kết quả
    result_file_name VARCHAR(500),
    status         export_status NOT NULL DEFAULT 'queued',
    error_message  TEXT,
    requested_at   TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    completed_at   TIMESTAMPTZ
);

-- ── READ-MODEL: tiến độ theo giáo viên (per subject) ─────────
CREATE TABLE marker_progress (
    id              UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    subject_id      UUID        NOT NULL,               -- cross-service ref
    teacher_id      UUID        NOT NULL,               -- cross-service ref
    assigned_count  INT         NOT NULL DEFAULT 0,
    completed_count INT         NOT NULL DEFAULT 0,
    drafting_count  INT         NOT NULL DEFAULT 0,
    avg_score       NUMERIC(5,2),
    throughput_per_hour NUMERIC(6,2),
    last_activity_at TIMESTAMPTZ,
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (subject_id, teacher_id)
);

-- ── READ-MODEL: tiến độ tổng theo môn ────────────────────────
CREATE TABLE subject_progress (
    id               UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    subject_id       UUID        NOT NULL UNIQUE,       -- cross-service ref
    total_papers     INT         NOT NULL DEFAULT 0,
    completed_papers INT         NOT NULL DEFAULT 0,
    in_progress      INT         NOT NULL DEFAULT 0,
    not_started      INT         NOT NULL DEFAULT 0,
    score_avg        NUMERIC(5,2),
    score_min        NUMERIC(5,2),
    score_max        NUMERIC(5,2),
    estimated_finish TIMESTAMPTZ,
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ── INDEXES ──────────────────────────────────────────────────
CREATE INDEX idx_export_jobs_subject   ON export_jobs(subject_id);
CREATE INDEX idx_export_jobs_status    ON export_jobs(status);
CREATE INDEX idx_marker_progress_subj  ON marker_progress(subject_id);
CREATE INDEX idx_marker_progress_tea   ON marker_progress(teacher_id);
