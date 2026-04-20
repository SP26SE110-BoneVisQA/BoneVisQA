-- BoneVisQA demo reset script (Supabase/PostgreSQL)
-- Date: 2026-04-19
-- Purpose: clear only main-flow data before the 21/04/2026 review session.
--
-- SAFETY:
--   1) Set "confirm_reset" to TRUE in the block below before running.
--   2) Verify target DB/project in Supabase SQL editor.
--   3) This script is destructive for listed tables.

BEGIN;

DO $$
DECLARE
    confirm_reset boolean := false; -- set TRUE to execute
BEGIN
    IF NOT confirm_reset THEN
        RAISE EXCEPTION
            'Reset blocked by safety guard. Edit script and set confirm_reset := true before execution.';
    END IF;
END $$;

-- Temporary suppress FK checks for controlled reset.
SET LOCAL session_replication_role = replica;

-- Keep users/roles/enrollments outside requested scope.
-- Includes typo alias from request ("annoucemnets") via actual table "announcements".
TRUNCATE TABLE
    case_annotations,
    case_answers,
    case_tags,
    case_view_logs,
    citations,
    document_chunks,
    documents,
    expert_reviews,
    medical_images,
    medical_cases,
    notifications,
    qa_messages,
    student_questions,
    visual_qa_sessions,
    announcements,
    academic_classes
RESTART IDENTITY CASCADE;

-- Optional cleanup for currently-unused table mentioned in audit request.
TRUNCATE TABLE
    class_tags
RESTART IDENTITY CASCADE;

SET LOCAL session_replication_role = origin;

COMMIT;

-- Quick verification (run separately if your SQL editor auto-stops after COMMIT):
-- SELECT 'academic_classes' AS table_name, count(*) FROM academic_classes
-- UNION ALL SELECT 'announcements', count(*) FROM announcements
-- UNION ALL SELECT 'case_annotations', count(*) FROM case_annotations
-- UNION ALL SELECT 'case_answers', count(*) FROM case_answers
-- UNION ALL SELECT 'case_tags', count(*) FROM case_tags
-- UNION ALL SELECT 'case_view_logs', count(*) FROM case_view_logs
-- UNION ALL SELECT 'citations', count(*) FROM citations
-- UNION ALL SELECT 'documents', count(*) FROM documents
-- UNION ALL SELECT 'document_chunks', count(*) FROM document_chunks
-- UNION ALL SELECT 'expert_reviews', count(*) FROM expert_reviews
-- UNION ALL SELECT 'medical_cases', count(*) FROM medical_cases
-- UNION ALL SELECT 'medical_images', count(*) FROM medical_images
-- UNION ALL SELECT 'notifications', count(*) FROM notifications
-- UNION ALL SELECT 'qa_messages', count(*) FROM qa_messages
-- UNION ALL SELECT 'student_questions', count(*) FROM student_questions
-- UNION ALL SELECT 'visual_qa_sessions', count(*) FROM visual_qa_sessions
-- UNION ALL SELECT 'class_tags', count(*) FROM class_tags;
