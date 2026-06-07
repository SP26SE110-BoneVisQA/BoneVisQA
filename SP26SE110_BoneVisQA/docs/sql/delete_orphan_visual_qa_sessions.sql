-- =============================================================================
-- BoneVisQA — Inspect / delete orphaned Visual QA sessions (no study image)
-- =============================================================================
-- Context: After fix_legacy_localhost_image_urls.sql SECTION 2B,
--   custom_image_url may be NULL while status stays 'Active'.
--   That is NOT a Postgres constraint error — it means the preview URL was
--   cleared because Storage files were deleted.
--
-- Use this script to:
--   1) Inspect whether a session still has a resolvable image via case_id
--   2) Optionally delete broken sessions (+ linked personal case if orphaned)
-- =============================================================================

-- ─── STEP 1 — Inspect the two sessions (replace IDs if needed) ───────────────

SELECT
    s.id AS session_id,
    s.status,
    s.custom_image_url,
    s.case_id,
    s.image_id,
    s.student_id,
    s.created_at,
    s.updated_at,
    (SELECT COUNT(*) FROM public.qa_messages m WHERE m.session_id = s.id) AS message_count,
    mi.image_url AS catalog_image_url,
    cm.media_url AS case_media_url,
    cm.storage_path AS case_media_storage_path,
    mc.owner_student_id AS personal_case_owner,
    mc.title AS case_title
FROM public.visual_qa_sessions s
LEFT JOIN public.medical_cases mc ON mc.id = s.case_id
LEFT JOIN public.medical_images mi ON mi.id = s.image_id
LEFT JOIN public.case_media cm ON cm.case_id = s.case_id
WHERE s.id IN (
    '1a48ff67-1e3f-41d5-8c91-89c9199c6931'::uuid,
    '363357f0-a0f3-4a3a-a9da-2d6df9f3c60b'::uuid
);

-- Interpretation:
--   custom_image_url IS NULL          → expected after SECTION 2B
--   status = 'Active'                 → still valid enum; NOT a DB bug
--   catalog_image_url / case_media_url empty or '' → no preview left
--   → session is "orphaned" for Visual QA viewer (chat history may remain)

-- ─── STEP 2 — List ALL orphaned Active personal sessions (optional) ─────────

SELECT
    s.id,
    s.student_id,
    s.case_id,
    s.updated_at,
    (SELECT COUNT(*) FROM public.qa_messages m WHERE m.session_id = s.id) AS msgs
FROM public.visual_qa_sessions s
LEFT JOIN public.medical_cases mc ON mc.id = s.case_id
WHERE s.status = 'Active'
  AND (s.custom_image_url IS NULL OR btrim(s.custom_image_url) = '')
  AND (
    s.case_id IS NULL
    OR NOT EXISTS (
        SELECT 1 FROM public.medical_images mi
        WHERE mi.case_id = s.case_id AND btrim(mi.image_url) <> ''
    )
  )
  AND (
    s.case_id IS NULL
    OR NOT EXISTS (
        SELECT 1 FROM public.case_media cm
        WHERE cm.case_id = s.case_id
          AND (
            (cm.media_url IS NOT NULL AND btrim(cm.media_url) <> '')
            OR (cm.storage_path IS NOT NULL AND btrim(cm.storage_path) <> '')
          )
    )
  )
ORDER BY s.updated_at DESC;

-- =============================================================================
-- STEP 3 — Delete broken sessions (recommended if no image + no chat to keep)
-- =============================================================================
-- Deletes:
--   visual_qa_sessions  → CASCADE qa_messages → CASCADE citations
--   expert_reviews.session_id → SET NULL (automatic)
--
-- Does NOT delete medical_cases (personal case may remain as empty shell).
-- Run STEP 4 only if you also want to remove the linked personal case.

BEGIN;

DELETE FROM public.visual_qa_sessions
WHERE id IN (
    '1a48ff67-1e3f-41d5-8c91-89c9199c6931'::uuid,
    '363357f0-a0f3-4a3a-a9da-2d6df9f3c60b'::uuid
);

COMMIT;

-- Verify gone
SELECT id FROM public.visual_qa_sessions
WHERE id IN (
    '1a48ff67-1e3f-41d5-8c91-89c9199c6931'::uuid,
    '363357f0-a0f3-4a3a-a9da-2d6df9f3c60b'::uuid
);

-- =============================================================================
-- STEP 4 — Optional: delete linked PERSONAL cases with no remaining sessions
-- =============================================================================
-- Only for cases where owner_student_id IS NOT NULL (student personal study).
-- Skip if the case is shared catalog (owner_student_id IS NULL).

/*
BEGIN;

-- Preview cases that would be deleted
SELECT mc.id, mc.title, mc.owner_student_id
FROM public.medical_cases mc
WHERE mc.owner_student_id IS NOT NULL
  AND mc.id IN (
      'PASTE-case_id-from-STEP-1'::uuid,
      'PASTE-case_id-from-STEP-1'::uuid
  )
  AND NOT EXISTS (
      SELECT 1 FROM public.visual_qa_sessions s WHERE s.case_id = mc.id
  );

DELETE FROM public.medical_cases mc
WHERE mc.owner_student_id IS NOT NULL
  AND mc.id IN (
      'PASTE-case_id-from-STEP-1'::uuid,
      'PASTE-case_id-from-STEP-1'::uuid
  )
  AND NOT EXISTS (
      SELECT 1 FROM public.visual_qa_sessions s WHERE s.case_id = mc.id
  );
-- CASCADE: case_media, medical_images, case_metadata, case_tags, etc.

COMMIT;
*/

-- =============================================================================
-- NOTES
-- =============================================================================
-- • You do NOT have to delete — user can ignore empty sidebar rows or upload
--   a new study (creates new session). Deleting only cleans the history list.
-- • If message_count > 0 and you want to keep chat text, do NOT delete.
-- • Re-uploading DICOM to the SAME session is not supported; upload creates
--   a new session via POST /api/student/visual-qa/upload-personal.
