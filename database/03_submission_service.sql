-- ============================================================
-- SUBMISSION SERVICE  (PostgreSQL + S3)   db: gradepaper_submission
-- Sở hữu: lô bài nộp (ZIP) + bài làm sinh viên (ẩn danh) + file bài làm.
-- CHỈ LƯU CON TRỎ S3 + metadata. KHÔNG parse, KHÔNG convert, KHÔNG OCR trong DB.
--   - Giáo viên click -> backend tạo pre-signed URL -> FE mở file theo content_type.
--   - Văn bản phục vụ AI: AI service đọc file từ S3 1 lần rồi embed vào Qdrant.
-- Quy ước S3: xem 00_storage_convention.md
-- Cross-service refs (no FK): subject_id -> Exam Catalog ; uploaded_by -> Identity.
-- ============================================================
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- ── ENUMS ────────────────────────────────────────────────────
CREATE TYPE batch_status AS ENUM (
    'uploaded',     -- ZIP vừa upload
    'extracting',   -- đang giải nén & tách file lên S3
    'ready',        -- tách xong, sẵn sàng phân công
    'failed'
);
CREATE TYPE paper_status AS ENUM (
    'ready_to_assign',  -- đã tách file, sẵn sàng chia
    'assigned',         -- đã chia cho giáo viên
    'completed',        -- đã chấm xong
    're_grading'        -- đang phúc khảo
);

-- ── SUBMISSION BATCHES (1 lần upload ZIP cho 1 môn) ──────────
CREATE TABLE submission_batches (
    id            UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    subject_id    UUID         NOT NULL,                -- cross-service ref
    zip_s3_key    TEXT         NOT NULL,                -- S3: file ZIP gốc
    zip_file_name VARCHAR(500),
    total_papers  INT          NOT NULL DEFAULT 0,
    status        batch_status NOT NULL DEFAULT 'uploaded',
    uploaded_by   UUID         NOT NULL,                -- user_id (admin) cross-service ref
    error_message TEXT,
    created_at    TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at    TIMESTAMPTZ
);

-- ── STUDENT PAPERS (1 bài làm = 1 SV ẩn danh) ────────────────
CREATE TABLE student_papers (
    id            UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    batch_id      UUID         NOT NULL REFERENCES submission_batches(id) ON DELETE CASCADE,
    subject_id    UUID         NOT NULL,                -- cross-service ref (tiện query)
    student_alias VARCHAR(255),                         -- alias chấm phách (tên hiển thị)
    alias_number  INT,                                  -- số alias để map dải phân công
    status        paper_status NOT NULL DEFAULT 'ready_to_assign',
    created_at    TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at    TIMESTAMPTZ,
    UNIQUE (batch_id, alias_number)
);

-- ── PAPER FILES (1 bài có thể nhiều file/trang) — chỉ là con trỏ S3 ──
CREATE TABLE paper_files (
    id               UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    student_paper_id UUID         NOT NULL REFERENCES student_papers(id) ON DELETE CASCADE,
    s3_key           TEXT         NOT NULL,             -- S3 object key
    file_name        VARCHAR(500),
    content_type     VARCHAR(120) NOT NULL,             -- application/pdf, image/png, text/plain...
    size_bytes       BIGINT,
    order_index      INT          NOT NULL DEFAULT 0,   -- thứ tự hiển thị (trang 1,2,...)
    created_at       TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

-- ── INDEXES ──────────────────────────────────────────────────
CREATE INDEX idx_batches_subject  ON submission_batches(subject_id);
CREATE INDEX idx_batches_status   ON submission_batches(status);
CREATE INDEX idx_papers_batch     ON student_papers(batch_id);
CREATE INDEX idx_papers_subject   ON student_papers(subject_id);
CREATE INDEX idx_papers_status    ON student_papers(status);
CREATE INDEX idx_paper_files_paper ON paper_files(student_paper_id);
