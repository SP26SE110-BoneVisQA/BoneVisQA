-- =============================================================================
-- BoneVisQA — Kiểm tra toàn bộ liên kết Visual QA ↔ Medical cases
-- Chạy trên Supabase SQL Editor (Production)
-- =============================================================================
-- API GET /api/student/visual-qa/history/{sessionId} trả 404 khi:
--   1) Không có row trong visual_qa_sessions với id đó, HOẶC
--   2) session.student_id ≠ user đang đăng nhập (JWT)
-- Xóa medical_cases KHÔNG nên xóa session (FK SET NULL case_id).
-- 404 thường = session đã bị DELETE thủ công, hoặc FE giữ sessionId cũ trên URL.
-- =============================================================================

-- ─── A0. Cột study_mode đã có chưa? (migration BE 20260608120000) ───────────

SELECT
    column_name,
    data_type,
    is_nullable,
    column_default
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name = 'visual_qa_sessions'
  AND column_name IN ('study_mode', 'case_id', 'promoted_case_id');

-- ─── A1. Kiểm tra MỘT session cụ thể ───────────────────────────────────────

SELECT
    'A1_session_exists' AS check_id,
    s.id AS session_id,
    s.student_id,
    u.email AS student_email,
    s.status,
    s.case_id,
    s.promoted_case_id,
    s.image_id,
    LEFT(s.custom_image_url, 80) AS custom_image_preview,
    s.created_at,
    s.updated_at,
    (SELECT COUNT(*) FROM public.qa_messages m WHERE m.session_id = s.id) AS message_count
FROM public.visual_qa_sessions s
LEFT JOIN public.users u ON u.id = s.student_id
WHERE s.id = 'eaa81cbd-a5b4-4ab3-adf7-1ea2fa011470'::uuid;

-- Nếu migration study_mode đã chạy, chạy thêm:
-- SELECT id, study_mode FROM public.visual_qa_sessions WHERE id = 'eaa81cbd-a5b4-4ab3-adf7-1ea2fa011470'::uuid;

-- Nếu 0 rows → session đã bị xóa → API 404 là đúng.
-- Nếu có row nhưng student_id khác account đang login → API 404 (quyền sở hữu).

-- ─── A2. Messages của session (nếu session còn tồn tại) ────────────────────

SELECT
    m.id AS message_id,
    m.role,
    LEFT(m.content, 120) AS content_preview,
    m.created_at,
    (SELECT COUNT(*) FROM public.citations c WHERE c.message_id = m.id) AS citation_count
FROM public.qa_messages m
WHERE m.session_id = 'eaa81cbd-a5b4-4ab3-adf7-1ea2fa011470'::uuid
ORDER BY m.created_at, m.id;

-- ─── A3. Messages mồ côi (session_id không còn session) ─────────────────────

SELECT
    'A3_orphan_qa_messages' AS check_id,
    m.session_id,
    COUNT(*) AS orphan_message_count,
    MIN(m.created_at) AS oldest,
    MAX(m.created_at) AS newest
FROM public.qa_messages m
WHERE NOT EXISTS (
    SELECT 1 FROM public.visual_qa_sessions s WHERE s.id = m.session_id
)
GROUP BY m.session_id
ORDER BY orphan_message_count DESC
LIMIT 50;

-- ─── B. FK constraints thực tế trên Supabase ────────────────────────────────

SELECT
    tc.constraint_name,
    tc.table_name,
    kcu.column_name,
    ccu.table_name AS foreign_table,
    rc.delete_rule
FROM information_schema.table_constraints tc
JOIN information_schema.key_column_usage kcu
    ON tc.constraint_name = kcu.constraint_name
    AND tc.table_schema = kcu.table_schema
JOIN information_schema.constraint_column_usage ccu
    ON ccu.constraint_name = tc.constraint_name
    AND ccu.table_schema = tc.table_schema
JOIN information_schema.referential_constraints rc
    ON rc.constraint_name = tc.constraint_name
    AND rc.constraint_schema = tc.table_schema
WHERE tc.constraint_type = 'FOREIGN KEY'
  AND tc.table_schema = 'public'
  AND (
    ccu.table_name = 'medical_cases'
    OR tc.table_name IN ('visual_qa_sessions', 'qa_messages', 'citations', 'expert_reviews')
  )
ORDER BY tc.table_name, tc.constraint_name;

-- Kỳ vọng:
--   visual_qa_sessions.case_id           → DELETE SET NULL
--   visual_qa_sessions.promoted_case_id  → DELETE SET NULL
--   qa_messages.session_id             → DELETE CASCADE (xóa session → xóa messages)
-- Nếu delete_rule = 'NO ACTION' hoặc 'RESTRICT' → cần chạy migration sửa FK.

-- ─── C. Medical cases còn bị block khi DELETE ───────────────────────────────

SELECT
    mc.id,
    mc.title,
    mc.is_approved,
    mc.owner_student_id,
    (SELECT COUNT(*) FROM public.visual_qa_sessions v WHERE v.case_id = mc.id) AS sessions_by_case_id,
    (SELECT COUNT(*) FROM public.visual_qa_sessions v WHERE v.promoted_case_id = mc.id) AS sessions_by_promoted,
    (SELECT COUNT(*) FROM public.class_cases cc WHERE cc.case_id = mc.id) AS class_case_links,
    (SELECT COUNT(*) FROM public.medical_images mi WHERE mi.case_id = mc.id) AS images,
    (SELECT COUNT(*) FROM public.case_media cm WHERE cm.case_id = mc.id) AS case_media_rows,
    (SELECT COUNT(*) FROM public.quiz_questions qq WHERE qq.case_id = mc.id) AS quiz_question_links,
    (SELECT COUNT(*) FROM public.student_questions sq WHERE sq.case_id = mc.id) AS student_questions
FROM public.medical_cases mc
ORDER BY sessions_by_case_id DESC, mc.created_at DESC;

-- ─── D. Sessions không còn medical case (case đã xóa / unlink) ──────────────

SELECT
    s.id AS session_id,
    s.student_id,
    s.case_id,
    s.status,
    s.custom_image_url IS NOT NULL AS has_custom_image,
    (SELECT COUNT(*) FROM public.qa_messages m WHERE m.session_id = s.id) AS msgs,
    s.updated_at
FROM public.visual_qa_sessions s
WHERE s.case_id IS NULL
ORDER BY s.updated_at DESC
LIMIT 100;

-- Catalog case study sessions sau khi xóa case (cần cột study_mode — xem A0):
-- SELECT s.id, s.student_id, s.study_mode, ...
-- FROM public.visual_qa_sessions s WHERE s.case_id IS NULL AND s.study_mode = 'catalog_case_study';

-- ─── E. Sessions trỏ tới case_id KHÔNG TỒN TẠI (dangling FK — lỗi schema) ─

SELECT
    s.id AS session_id,
    s.case_id AS missing_case_id,
    s.student_id,
    s.status,
    (SELECT COUNT(*) FROM public.qa_messages m WHERE m.session_id = s.id) AS msgs
FROM public.visual_qa_sessions s
WHERE s.case_id IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM public.medical_cases mc WHERE mc.id = s.case_id);

-- ─── F. image_id trỏ medical_images đã mất ─────────────────────────────────

SELECT
    s.id AS session_id,
    s.image_id AS missing_image_id,
    s.case_id
FROM public.visual_qa_sessions s
WHERE s.image_id IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM public.medical_images mi WHERE mi.id = s.image_id);

-- ─── G. expert_reviews / citations gắn session ─────────────────────────────

SELECT
    er.id AS expert_review_id,
    er.session_id,
    er.answer_id,
    er.status,
    er.created_at
FROM public.expert_reviews er
WHERE er.session_id = 'eaa81cbd-a5b4-4ab3-adf7-1ea2fa011470'::uuid
   OR er.session_id IN (
       SELECT s.id FROM public.visual_qa_sessions s
       WHERE s.case_id IS NULL AND s.updated_at > NOW() - INTERVAL '30 days'
   );

-- ─── H. Tổng quan nhanh ─────────────────────────────────────────────────────

SELECT 'visual_qa_sessions' AS entity, COUNT(*) AS cnt FROM public.visual_qa_sessions
UNION ALL SELECT 'qa_messages', COUNT(*) FROM public.qa_messages
UNION ALL SELECT 'citations (message)', COUNT(*) FROM public.citations WHERE message_id IS NOT NULL
UNION ALL SELECT 'medical_cases', COUNT(*) FROM public.medical_cases
UNION ALL SELECT 'orphan qa_messages', COUNT(*)
FROM public.qa_messages m
WHERE NOT EXISTS (SELECT 1 FROM public.visual_qa_sessions s WHERE s.id = m.session_id);

-- =============================================================================
-- CLEANUP (chỉ chạy sau khi đã inspect — bỏ comment từng block)
-- =============================================================================

-- H1. Xóa qa_messages mồ côi (session đã mất)
/*
BEGIN;
DELETE FROM public.qa_messages m
WHERE NOT EXISTS (
    SELECT 1 FROM public.visual_qa_sessions s WHERE s.id = m.session_id
);
COMMIT;
*/

-- H2. Unlink sessions trước khi xóa medical case (an toàn)
/*
BEGIN;
UPDATE public.visual_qa_sessions
SET case_id = NULL
WHERE case_id = 'PASTE-medical-case-uuid'::uuid;

UPDATE public.visual_qa_sessions
SET promoted_case_id = NULL
WHERE promoted_case_id = 'PASTE-medical-case-uuid'::uuid;
COMMIT;
*/

-- H3. Xóa session Visual QA (+ CASCADE qa_messages)
/*
BEGIN;
DELETE FROM public.visual_qa_sessions
WHERE id = 'eaa81cbd-a5b4-4ab3-adf7-1ea2fa011470'::uuid;
COMMIT;
*/

-- H4. Sửa FK visual_qa_sessions.case_id → ON DELETE SET NULL (nếu inspect B thấy RESTRICT)
/*
ALTER TABLE public.visual_qa_sessions DROP CONSTRAINT IF EXISTS vqs_case_fk;
ALTER TABLE public.visual_qa_sessions DROP CONSTRAINT IF EXISTS visual_qa_sessions_case_id_fkey;
ALTER TABLE public.visual_qa_sessions ALTER COLUMN case_id DROP NOT NULL;
ALTER TABLE public.visual_qa_sessions
  ADD CONSTRAINT visual_qa_sessions_case_id_fkey
  FOREIGN KEY (case_id) REFERENCES public.medical_cases (id) ON DELETE SET NULL;

ALTER TABLE public.visual_qa_sessions DROP CONSTRAINT IF EXISTS FK_visual_qa_sessions_medical_cases_promoted_case_id;
ALTER TABLE public.visual_qa_sessions
  ADD CONSTRAINT FK_visual_qa_sessions_medical_cases_promoted_case_id
  FOREIGN KEY (promoted_case_id) REFERENCES public.medical_cases (id) ON DELETE SET NULL;
*/

-- H5. Thêm study_mode (nếu cột chưa có — migration BE 20260608120000)
/*
ALTER TABLE public.visual_qa_sessions
  ADD COLUMN IF NOT EXISTS study_mode varchar(32) NOT NULL DEFAULT 'personal_dicom';

UPDATE public.visual_qa_sessions vqs
SET study_mode = 'catalog_case_study'
FROM public.medical_cases mc
WHERE vqs.case_id = mc.id
  AND mc.is_approved = true
  AND mc.owner_student_id IS NULL
  AND mc.created_by_expert_id IS NOT NULL;

UPDATE public.visual_qa_sessions
SET study_mode = 'catalog_case_study'
WHERE case_id IS NULL
  AND study_mode = 'personal_dicom'
  AND EXISTS (
    SELECT 1 FROM public.qa_messages m WHERE m.session_id = visual_qa_sessions.id
  );
-- (điều chỉnh thủ công nếu cần phân biệt orphan catalog vs personal)
*/
