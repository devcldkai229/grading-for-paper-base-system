-- ============================================================
-- EXAM CATALOG SERVICE  (PostgreSQL)   db: gradepaper_exam_catalog
-- Sở hữu: danh mục thi Semester → Exam → Subject + lưới điểm (questions).
-- ĐỀ THI & BAREM: chỉ lưu 1 file/loại trên S3 để giáo viên click XEM.
--   -> KHÔNG parse nội dung vào DB. Nội dung cho AI nằm ở Qdrant (xem AI service).
-- Quy ước S3: xem 00_storage_convention.md
-- Cross-service: KHÔNG FK ra ngoài.
-- ============================================================
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- ── ENUMS ────────────────────────────────────────────────────
CREATE TYPE exam_type      AS ENUM ('FE', 'PE', 'PT', 'OTHER');  -- Final/Practical/Progress
CREATE TYPE subject_status AS ENUM ('draft', 'open', 'grading', 'closed');

-- ── SEMESTERS ────────────────────────────────────────────────
CREATE TABLE semesters (
    id          UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    code        VARCHAR(20)  NOT NULL UNIQUE,           -- SP26, FA25
    name        VARCHAR(100) NOT NULL,
    description TEXT,
    start_date  DATE         NOT NULL,
    end_date    DATE         NOT NULL,
    is_active   BOOLEAN      NOT NULL DEFAULT FALSE,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ,
    CONSTRAINT chk_semester_dates CHECK (end_date > start_date)
);

-- ── EXAMS (kì thi) ───────────────────────────────────────────
CREATE TABLE exams (
    id          UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    semester_id UUID         NOT NULL REFERENCES semesters(id) ON DELETE CASCADE,
    name        VARCHAR(255) NOT NULL,                  -- "FE #1", "PE #2"
    exam_type   exam_type    NOT NULL DEFAULT 'FE',
    start_date  DATE,
    end_date    DATE,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ
);

-- ── SUBJECTS (môn thi) — đơn vị chấm chính ───────────────────
-- Mỗi môn chỉ đính kèm: 1 file đề thi + 1 file barem (trên S3, để xem).
CREATE TABLE subjects (
    id                   UUID           PRIMARY KEY DEFAULT gen_random_uuid(),
    exam_id              UUID           NOT NULL REFERENCES exams(id) ON DELETE CASCADE,
    subject_code         VARCHAR(50)    NOT NULL,       -- PMG201c
    title                VARCHAR(255),
    max_score            NUMERIC(5,2)   NOT NULL DEFAULT 10 CHECK (max_score > 0),
    -- ĐỀ THI: 1 file (pdf/docx/image) — chỉ để hiển thị
    exam_paper_s3_key       TEXT,
    exam_paper_file_name    VARCHAR(500),
    exam_paper_content_type VARCHAR(120),
    exam_paper_preview_s3_key       TEXT,
    exam_paper_preview_content_type VARCHAR(120),
    -- BAREM: 1 file (pdf/docx/image) — chỉ để hiển thị
    rubric_s3_key           TEXT,
    rubric_file_name        VARCHAR(500),
    rubric_content_type     VARCHAR(120),
    rubric_preview_s3_key           TEXT,
    rubric_preview_content_type     VARCHAR(120),
    rubric_version          INT          NOT NULL DEFAULT 1,  -- tăng khi upload barem mới (để AI re-index Qdrant)
    status               subject_status NOT NULL DEFAULT 'draft',
    created_at           TIMESTAMPTZ    NOT NULL DEFAULT NOW(),
    updated_at           TIMESTAMPTZ,
    UNIQUE (exam_id, subject_code)
);

-- ── QUESTIONS = LƯỚI ĐIỂM (không phải nội dung đề/barem) ──────
-- Chỉ định nghĩa: môn có mấy câu, mỗi câu tối đa bao nhiêu điểm.
-- Dùng để dựng form chấm và validate tổng điểm. Admin nhập khi tạo môn.
CREATE TABLE questions (
    id              UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    subject_id      UUID         NOT NULL REFERENCES subjects(id) ON DELETE CASCADE,
    question_number VARCHAR(20)  NOT NULL,              -- "1","2","Request 1"...
    group_label     VARCHAR(100),                       -- nhãn nhóm hiển thị (vd "Req 1"); NULL = không nhóm
    label           VARCHAR(255),                       -- nhãn ngắn (tuỳ chọn)
    max_score       NUMERIC(5,2) NOT NULL CHECK (max_score > 0),
    order_index     INT          NOT NULL DEFAULT 0,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    UNIQUE (subject_id, question_number)
);

-- ── INDEXES ──────────────────────────────────────────────────
CREATE INDEX idx_exams_semester    ON exams(semester_id);
CREATE INDEX idx_subjects_exam     ON subjects(exam_id);
CREATE INDEX idx_subjects_status   ON subjects(status);
CREATE INDEX idx_questions_subject ON questions(subject_id);
