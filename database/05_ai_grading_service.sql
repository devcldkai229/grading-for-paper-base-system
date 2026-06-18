-- ============================================================
-- AI GRADING SERVICE  (PostgreSQL + Qdrant)   db: gradepaper_ai
-- Sở hữu: kết quả AI + gợi ý điểm + theo dõi index Qdrant.
--   - Qdrant: NƠI CHỨA NỘI DUNG cho AI (barem, đề, bài mẫu, đoạn bài làm) đã embed sẵn
--             -> AI truy hồi nhanh, KHÔNG phải đọc lại file S3 mỗi lần chấm.
--   - PostgreSQL (file này): chỉ lưu kết quả & metadata vận hành.
-- Luồng: nhận job (RabbitMQ) -> truy hồi context từ Qdrant -> LLM chấm
--        -> ghi gợi ý về Grading (gRPC).
-- Cross-service refs (no FK): grading_assignment_id, subject_id, student_paper_id.
-- ============================================================
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- ── ENUMS ────────────────────────────────────────────────────
CREATE TYPE ai_grading_status AS ENUM ('queued', 'processing', 'completed', 'failed');
CREATE TYPE index_status      AS ENUM ('pending', 'indexing', 'indexed', 'failed');

-- ── RUBRIC INDEX STATUS (theo dõi việc embed barem/đề vào Qdrant) ──
-- Mỗi môn + phiên bản barem được embed 1 lần; lần sau dùng lại trong Qdrant.
CREATE TABLE rubric_index_status (
    id              UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    subject_id      UUID         NOT NULL,              -- cross-service ref
    rubric_version  INT          NOT NULL,
    qdrant_collection VARCHAR(120) NOT NULL,            -- vd: rubric_exemplars
    point_count     INT          NOT NULL DEFAULT 0,    -- số vector đã nạp
    status          index_status NOT NULL DEFAULT 'pending',
    error_message   TEXT,
    indexed_at      TIMESTAMPTZ,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    UNIQUE (subject_id, rubric_version)
);

-- ── AI GRADING RESULTS (1 lần AI chạy cho 1 assignment) ──────
CREATE TABLE ai_grading_results (
    id                    UUID              PRIMARY KEY DEFAULT gen_random_uuid(),
    grading_assignment_id UUID              NOT NULL,   -- cross-service ref
    student_paper_id      UUID              NOT NULL,   -- cross-service ref
    subject_id            UUID              NOT NULL,   -- cross-service ref
    rubric_version        INT               NOT NULL DEFAULT 1,
    -- false nếu bị rule-based filter chặn (điểm bất thường / confidence quá thấp)
    is_valid_result       BOOLEAN           NOT NULL DEFAULT TRUE,
    model_used            VARCHAR(100),                 -- 'gpt-4o','claude-sonnet-4-6'
    input_tokens          INT,
    output_tokens         INT,
    duration_ms           INT,
    status                ai_grading_status NOT NULL DEFAULT 'queued',
    error_message         TEXT,
    created_at            TIMESTAMPTZ       NOT NULL DEFAULT NOW(),
    completed_at          TIMESTAMPTZ,
    UNIQUE (grading_assignment_id)                      -- giữ kết quả mới nhất / assignment
);

-- ── AI QUESTION SUGGESTIONS (gợi ý từng câu) ─────────────────
CREATE TABLE ai_question_suggestions (
    id                   UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    ai_grading_result_id UUID         NOT NULL
                                      REFERENCES ai_grading_results(id) ON DELETE CASCADE,
    question_number      VARCHAR(20)  NOT NULL,
    suggested_score      NUMERIC(5,2) NOT NULL,
    explanation          TEXT         NOT NULL,         -- vì sao AI cho điểm này
    evidence_extracted   TEXT,                          -- trích dẫn từ bài làm
    confidence           NUMERIC(4,3) CHECK (confidence BETWEEN 0 AND 1),
    created_at           TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    UNIQUE (ai_grading_result_id, question_number)
);

-- ── INDEXES ──────────────────────────────────────────────────
CREATE INDEX idx_rubric_index_subject  ON rubric_index_status(subject_id);
CREATE INDEX idx_ai_results_assignment ON ai_grading_results(grading_assignment_id);
CREATE INDEX idx_ai_results_subject    ON ai_grading_results(subject_id);
CREATE INDEX idx_ai_results_status     ON ai_grading_results(status);
CREATE INDEX idx_ai_suggestions_result ON ai_question_suggestions(ai_grading_result_id);

-- ── GHI CHÚ: Qdrant collection `rubric_exemplars` ────────────
-- point = {
--   id: uuid,
--   vector: [ ... embedding ... ],
--   payload: {
--     subjectId, rubricVersion, kind: 'rubric'|'exam'|'exemplar'|'answer_chunk',
--     questionNumber, text, awardedScore?    -- text = đoạn nội dung để AI đọc trực tiếp
--   }
-- }
-- Nhờ payload.text, AI lấy ngay nội dung từ Qdrant, không phải tải lại file từ S3.
-- (Khối lượng nhỏ có thể thay Qdrant bằng pgvector ngay trong DB này.)
