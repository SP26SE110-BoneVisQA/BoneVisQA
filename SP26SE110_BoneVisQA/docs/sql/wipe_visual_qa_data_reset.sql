-- =============================================================================
-- BoneVisQA — XÓA SẠCH dữ liệu Visual QA để làm mới (Supabase SQL Editor)
-- =============================================================================
-- SAO LƯU: Không thể hoàn tác sau COMMIT. Chạy PREVIEW trước, rồi mới WIPE.
--
-- KHÔNG xóa: users, medical_cases, document_chunks, quizzes, class_cases, ...
-- SAU WIPE: student upload DICOM / mở case mới → tạo session + messages mới bình thường.
-- =============================================================================

-- ─── PREVIEW — đếm trước khi xóa ───────────────────────────────────────────

SELECT 'visual_qa_sessions' AS table_name, COUNT(*) AS row_count FROM public.visual_qa_sessions
UNION ALL SELECT 'qa_messages', COUNT(*) FROM public.qa_messages
UNION ALL SELECT 'citations (all)', COUNT(*) FROM public.citations
UNION ALL SELECT 'citations (visual QA only)', COUNT(*)
    FROM public.citations c WHERE c.message_id IS NOT NULL
UNION ALL SELECT 'citations (case_answers only)', COUNT(*)
    FROM public.citations c WHERE c.answer_id IS NOT NULL AND c.message_id IS NULL
UNION ALL SELECT 'expert_reviews (all)', COUNT(*) FROM public.expert_reviews
UNION ALL SELECT 'expert_reviews (visual QA session)', COUNT(*)
    FROM public.expert_reviews er WHERE er.session_id IS NOT NULL
UNION ALL SELECT 'qa_messages orphan (no session)', COUNT(*)
    FROM public.qa_messages m
    WHERE NOT EXISTS (SELECT 1 FROM public.visual_qa_sessions s WHERE s.id = m.session_id);

-- =============================================================================
-- OPTION 1 — CHỈ Visual QA (khuyến nghị)
-- Giữ citations / expert_reviews của Case Q&A (case_answers, lecturer triage)
-- =============================================================================

/*
BEGIN;

-- 1) Citations gắn qa_messages (Visual QA chat)
DELETE FROM public.citations c
WHERE c.message_id IS NOT NULL;

-- 2) Citations mồ côi (message đã mất trước đó)
DELETE FROM public.citations c
WHERE c.message_id IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM public.qa_messages m WHERE m.id = c.message_id);

-- 3) Expert reviews gắn visual_qa_sessions
DELETE FROM public.expert_reviews er
WHERE er.session_id IS NOT NULL;

-- 4) Gỡ pointer review trên session (nếu có)
UPDATE public.visual_qa_sessions
SET requested_review_message_id = NULL
WHERE requested_review_message_id IS NOT NULL;

-- 5) Messages mồ côi (session đã xóa trước đó)
DELETE FROM public.qa_messages m
WHERE NOT EXISTS (
    SELECT 1 FROM public.visual_qa_sessions s WHERE s.id = m.session_id
);

-- 6) Toàn bộ sessions → CASCADE xóa qa_messages còn lại (nếu FK CASCADE đúng)
DELETE FROM public.visual_qa_sessions;

COMMIT;
*/

-- =============================================================================
-- OPTION 2 — XÓA TOÀN BỘ 4 BẢNG (nuclear reset)
-- Cảnh báo: mất LUÔN citations của case_answers + mọi expert_reviews
-- =============================================================================

/*
BEGIN;

DELETE FROM public.citations;
DELETE FROM public.expert_reviews;
DELETE FROM public.qa_messages;
DELETE FROM public.visual_qa_sessions;

COMMIT;
*/

-- =============================================================================
-- VERIFY — chạy sau WIPE (kỳ vọng tất cả = 0) ───────────────────────────────

SELECT 'visual_qa_sessions' AS table_name, COUNT(*) AS remaining FROM public.visual_qa_sessions
UNION ALL SELECT 'qa_messages', COUNT(*) FROM public.qa_messages
UNION ALL SELECT 'citations', COUNT(*) FROM public.citations
UNION ALL SELECT 'expert_reviews', COUNT(*) FROM public.expert_reviews;

-- =============================================================================
-- GHI CHÚ SAU WIPE
-- =============================================================================
-- • FE: refresh trang, xóa sessionId cũ khỏi URL (?sessionId=...).
-- • History sidebar trống → upload DICOM mới hoặc mở case library mới.
-- • medical_cases vẫn còn — nếu muốn xóa case cũ, dùng script unlink FK trước
--   (inspect_visual_qa_medical_case_integrity.sql Block H2/H4).
-- • study_mode: chạy thêm migration H5 nếu chưa có cột (trước khi deploy BE mới).
