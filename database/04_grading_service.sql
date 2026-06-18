-- ============================================================
-- GRADING SERVICE  (PostgreSQL + Redis)  db: gradepaper_grading
-- Owns: phân công chấm, phiên chấm, điểm từng câu/ý, feedback, audit.
--   - Redis: distributed lock (1 GV/1 bài), cache barem, idempotency.
-- Cross-service refs (no FK):
--   user_id/teacher_id -> Identity ; subject_id/student_paper_id -> Submission/Exam.
-- AI suggestions là nguồn của AI Service; ở đây chỉ lưu QUYẾT ĐỊNH của GV.
-- ============================================================
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- ── ENUMS ────────────────────────────────────────────────────
CREATE TYPE assignment_type AS ENUM (
    'first_grade',   -- chấm lần đầu
    'cross_grade',   -- chấm chéo (2 GV chấm độc lập)
    're_grade'       -- phúc khảo
);
CREATE TYPE grading_progress_status AS ENUM (
    'not_started',   -- chưa mở bài
    'drafting',      -- đang chấm, lưu nháp
    'submitted'      -- đã chốt (lock) — chỉ admin unlock
);
-- Bản sao read-only trạng thái AI (đồng bộ từ AI Service qua gRPC/message)
CREATE TYPE ai_sync_status AS ENUM (
    'not_requested', 'queued', 'processing', 'completed', 'failed'
);
-- Quyết định của GV với gợi ý AI cho từng câu
CREATE TYPE ai_review_status AS ENUM (
    'pending',   -- GV chưa xem
    'accepted',  -- chấp nhận gợi ý AI
    'rejected',  -- từ chối, tự nhập
    'modified'   -- sửa lại từ gợi ý AI
);

-- ── MARKER ASSIGNMENTS (admin chia dải alias cho GV theo môn) ─
CREATE TABLE marker_assignments (
    id           UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    subject_id   UUID        NOT NULL,                  -- cross-service ref
    teacher_id   UUID        NOT NULL,                  -- user_id cross-service ref
    alias_start  INT         NOT NULL CHECK (alias_start >= 1),
    alias_end    INT         NOT NULL CHECK (alias_end >= alias_start),
    assigned_by  UUID        NOT NULL,                  -- admin user_id
    assigned_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (subject_id, alias_start, alias_end)
);

-- ── GRADING ASSIGNMENTS (ai chấm bài nào, loại gì) ───────────
CREATE TABLE grading_assignments (
    id               UUID                    PRIMARY KEY DEFAULT gen_random_uuid(),
    student_paper_id UUID                    NOT NULL,  -- cross-service ref
    subject_id       UUID                    NOT NULL,  -- cross-service ref (tiện query)
    teacher_id       UUID                    NOT NULL,  -- cross-service ref
    assignment_type  assignment_type         NOT NULL DEFAULT 'first_grade',
    status           grading_progress_status NOT NULL DEFAULT 'not_started',
    ai_status        ai_sync_status          NOT NULL DEFAULT 'not_requested',
    created_at       TIMESTAMPTZ             NOT NULL DEFAULT NOW(),
    updated_at       TIMESTAMPTZ,
    UNIQUE (student_paper_id, teacher_id, assignment_type)
);

-- ── GRADING FORMS (kết quả chấm, 1-1 với assignment) ─────────
CREATE TABLE grading_forms (
    id                    UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    grading_assignment_id UUID         NOT NULL UNIQUE
                                       REFERENCES grading_assignments(id) ON DELETE CASCADE,
    total_score           NUMERIC(5,2) NOT NULL DEFAULT 0,
    paper_comment         TEXT,        -- nhận xét bài làm (có thể hiển thị cho SV)
    general_comment       TEXT,        -- nhận xét tổng thể (dùng khi chấm chéo)
    internal_comment      TEXT,        -- nội bộ GV+Admin, KHÔNG export ra XLSX
    submitted_at          TIMESTAMPTZ,
    -- optimistic concurrency để chống lost-update khi autosave
    row_version           INT          NOT NULL DEFAULT 0,
    created_at            TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at            TIMESTAMPTZ
);

-- ── QUESTION GRADE DETAILS (điểm từng câu — core màn hình) ────
CREATE TABLE question_grade_details (
    id               UUID             PRIMARY KEY DEFAULT gen_random_uuid(),
    grading_form_id  UUID             NOT NULL REFERENCES grading_forms(id) ON DELETE CASCADE,
    question_number  VARCHAR(20)      NOT NULL,         -- map với câu hỏi của môn
    score            NUMERIC(5,2)     NOT NULL DEFAULT 0,
    max_score        NUMERIC(5,2)     NOT NULL,         -- denormalized cho UI
    question_comment TEXT,
    ai_drafted       BOOLEAN          NOT NULL DEFAULT FALSE,  -- điểm khởi tạo từ AI?
    ai_review_status ai_review_status NOT NULL DEFAULT 'pending',
    created_at       TIMESTAMPTZ      NOT NULL DEFAULT NOW(),
    updated_at       TIMESTAMPTZ,
    UNIQUE (grading_form_id, question_number),
    CONSTRAINT chk_score_range CHECK (score >= 0 AND score <= max_score)
);

-- ── AUDIT LOGS (mọi thay đổi điểm — phục vụ khiếu nại) ───────
CREATE TABLE audit_logs (
    id          UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     UUID         NOT NULL,                  -- cross-service ref
    action      VARCHAR(100) NOT NULL,                  -- 'ScoreUpdated','FormSubmitted','AIAccepted'
    entity_type VARCHAR(100) NOT NULL,                  -- 'QuestionGradeDetail','GradingForm'
    entity_id   UUID,
    old_value   JSONB,
    new_value   JSONB,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

-- ── INDEXES ──────────────────────────────────────────────────
CREATE INDEX idx_marker_assign_subject  ON marker_assignments(subject_id);
CREATE INDEX idx_marker_assign_teacher  ON marker_assignments(teacher_id);
CREATE INDEX idx_assign_paper           ON grading_assignments(student_paper_id);
CREATE INDEX idx_assign_teacher         ON grading_assignments(teacher_id);
CREATE INDEX idx_assign_subject         ON grading_assignments(subject_id);
CREATE INDEX idx_assign_status          ON grading_assignments(status);
CREATE INDEX idx_assign_ai_status       ON grading_assignments(ai_status);
CREATE INDEX idx_grade_details_form     ON question_grade_details(grading_form_id);
CREATE INDEX idx_audit_entity           ON audit_logs(entity_type, entity_id);
CREATE INDEX idx_audit_user             ON audit_logs(user_id);
