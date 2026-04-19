-- Schema Migration History
-- Ngày: 2026-04-19
-- Migrations đã được apply thủ công (không dùng EF migrations)

-- ============================================
-- MIGRATION: AddAssignmentIdToAnnouncements
-- ============================================

-- announcements: thêm assignment_id
ALTER TABLE announcements ADD COLUMN IF NOT EXISTS assignment_id UUID NULL;
CREATE INDEX IF NOT EXISTS idx_announcements_assignment ON announcements(assignment_id);

-- ============================================
-- CÁC MIGRATIONS TRƯỚC ĐÓ ĐÃ APPLY TRONG apply_remaining_schema.sql
-- ============================================

-- 1. visual_qa_sessions: promoted_case_id
ALTER TABLE visual_qa_sessions ADD COLUMN IF NOT EXISTS promoted_case_id UUID NULL;
CREATE INDEX IF NOT EXISTS ix_visual_qa_sessions_promoted_case_id ON visual_qa_sessions(promoted_case_id);
ALTER TABLE visual_qa_sessions 
    ADD CONSTRAINT fk_visual_qa_sessions_medical_cases_promoted_case_id 
    FOREIGN KEY (promoted_case_id) REFERENCES medical_cases(id) ON DELETE SET NULL;

-- 2. student_quiz_answers: các column mới
ALTER TABLE student_quiz_answers ADD COLUMN IF NOT EXISTS essay_answer TEXT NULL;
ALTER TABLE student_quiz_answers ADD COLUMN IF NOT EXISTS graded_at TIMESTAMP WITH TIME ZONE NULL;
ALTER TABLE student_quiz_answers ADD COLUMN IF NOT EXISTS graded_by UUID NULL;
ALTER TABLE student_quiz_answers ADD COLUMN IF NOT EXISTS is_graded BOOLEAN NOT NULL DEFAULT false;
ALTER TABLE student_quiz_answers ADD COLUMN IF NOT EXISTS lecturer_feedback TEXT NULL;
ALTER TABLE student_quiz_answers ADD COLUMN IF NOT EXISTS score_awarded NUMERIC(5,2) NULL;
CREATE INDEX IF NOT EXISTS ix_student_quiz_answers_graded_by ON student_quiz_answers(graded_by);
ALTER TABLE student_quiz_answers 
    ADD CONSTRAINT student_quiz_answers_graded_by_fkey 
    FOREIGN KEY (graded_by) REFERENCES users(id) ON DELETE SET NULL;

-- 3. quizzes: mode
ALTER TABLE quizzes ADD COLUMN IF NOT EXISTS mode TEXT NULL DEFAULT 'multiple_choice';

-- 4. quiz_questions: type (text -> int), max_score, reference_answer
-- Chuyển type từ text sang int
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns 
               WHERE table_name='quiz_questions' AND column_name='type' 
               AND data_type='text') THEN
        ALTER TABLE quiz_questions ADD COLUMN type_new INTEGER NULL;
        UPDATE quiz_questions SET type_new = 
            CASE 
                WHEN type = 'MultipleChoice' THEN 1
                WHEN type = 'TrueFalse' THEN 2
                WHEN type = 'Essay' THEN 3
                ELSE NULL
            END;
        ALTER TABLE quiz_questions DROP COLUMN type;
        ALTER TABLE quiz_questions RENAME COLUMN type_new TO type;
    END IF;
END $$;
ALTER TABLE quiz_questions ADD COLUMN IF NOT EXISTS max_score INTEGER NOT NULL DEFAULT 1;
ALTER TABLE quiz_questions ADD COLUMN IF NOT EXISTS reference_answer TEXT NULL;

-- 5. qa_messages: citations_json, client_request_id
ALTER TABLE qa_messages ADD COLUMN IF NOT EXISTS citations_json JSONB NULL;
ALTER TABLE qa_messages ADD COLUMN IF NOT EXISTS client_request_id TEXT NULL;

-- 6. documents: version -> text, các column indexing
ALTER TABLE documents ALTER COLUMN version TYPE TEXT;
ALTER TABLE documents ADD COLUMN IF NOT EXISTS current_page_indexing INTEGER NULL;
ALTER TABLE documents ADD COLUMN IF NOT EXISTS pending_reindex_hash TEXT NULL;
ALTER TABLE documents ADD COLUMN IF NOT EXISTS pending_reindex_path TEXT NULL;
ALTER TABLE documents ADD COLUMN IF NOT EXISTS pending_target_version TEXT NULL;
ALTER TABLE documents ADD COLUMN IF NOT EXISTS total_chunks INTEGER NULL;
ALTER TABLE documents ADD COLUMN IF NOT EXISTS total_pages INTEGER NOT NULL DEFAULT 0;
ALTER TABLE documents ADD COLUMN IF NOT EXISTS updated_at TIMESTAMP WITH TIME ZONE NULL;

-- 7. document_chunks: start_page, end_page
ALTER TABLE document_chunks ADD COLUMN IF NOT EXISTS start_page INTEGER NULL;
ALTER TABLE document_chunks ADD COLUMN IF NOT EXISTS end_page INTEGER NULL;

-- 8. citations: message_id + index + FK
ALTER TABLE citations ADD COLUMN IF NOT EXISTS message_id UUID NULL;
CREATE INDEX IF NOT EXISTS ix_citations_message_id ON citations(message_id);
ALTER TABLE citations 
    ADD CONSTRAINT fk_citations_qa_messages_message_id 
    FOREIGN KEY (message_id) REFERENCES qa_messages(id) ON DELETE SET NULL;

-- 9. quizzes: assigned_expert_id FK
ALTER TABLE quizzes 
    ADD CONSTRAINT quizzes_assigned_by_expert_id_fkey 
    FOREIGN KEY (assigned_expert_id) REFERENCES users(id) ON DELETE SET NULL;

SELECT 'All schema migrations applied successfully' AS result;
