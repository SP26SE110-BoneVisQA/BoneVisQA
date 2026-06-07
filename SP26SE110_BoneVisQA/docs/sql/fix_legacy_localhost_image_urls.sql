-- =============================================================================
-- BoneVisQA — Fix legacy localhost /uploads/ image URLs in Supabase Postgres
-- =============================================================================
-- Problem:
--   Dev BE stored URLs like:
--     https://localhost:5047/uploads/images/IMG000037_20260510153844.jpeg
--   Production FE cannot load these (ERR_CONNECTION_REFUSED).
--
-- Two remediation paths (pick ONE):
--
--   SECTION 2A — Rewrite URLs to Supabase public paths
--     Use ONLY if the object still exists in Storage bucket `medical-images`
--     (e.g. path `images/IMG000037_....jpeg`).
--
--   SECTION 2B — Clear broken legacy URLs (RECOMMENDED if Storage files were deleted)
--     Sets localhost / legacy /uploads/ references to NULL or empty string.
--     Stops console ERR_CONNECTION_REFUSED; FE should show image placeholder.
--     User must re-upload DICOM study / images to restore previews.
--
-- Run in: Supabase Dashboard → SQL Editor
-- Date: 2026-06-07
-- =============================================================================

-- ─── Configuration ───────────────────────────────────────────────────────────
-- Supabase project URL (for SECTION 2A only):
--   https://jshryhplbayoymtthqpu.supabase.co

-- =============================================================================
-- SECTION 1 — Diagnostics (read-only)
-- =============================================================================

-- 1a. Count rows with localhost dev URLs per table
SELECT 'medical_images' AS table_name, COUNT(*) AS affected
FROM public.medical_images
WHERE image_url ILIKE '%localhost:5047%'
   OR image_url ILIKE '%localhost:5046%'
   OR image_url ILIKE 'http://localhost%'
   OR image_url ILIKE 'https://localhost%'

UNION ALL
SELECT 'visual_qa_sessions', COUNT(*)
FROM public.visual_qa_sessions
WHERE custom_image_url ILIKE '%localhost:5047%'
   OR custom_image_url ILIKE '%localhost:5046%'
   OR custom_image_url ILIKE 'http://localhost%'
   OR custom_image_url ILIKE 'https://localhost%'

UNION ALL
SELECT 'case_media (media_url)', COUNT(*)
FROM public.case_media
WHERE media_url ILIKE '%localhost:5047%'
   OR media_url ILIKE '%localhost:5046%'
   OR media_url ILIKE 'http://localhost%'
   OR media_url ILIKE 'https://localhost%'

UNION ALL
SELECT 'case_media (storage_path)', COUNT(*)
FROM public.case_media
WHERE storage_path ILIKE '%localhost%'
   OR storage_path ILIKE '/uploads/%'

UNION ALL
SELECT 'case_media (dicom_metadata jsonb)', COUNT(*)
FROM public.case_media
WHERE dicom_metadata IS NOT NULL
  AND (
    dicom_metadata::text ILIKE '%localhost:5047%'
    OR dicom_metadata::text ILIKE '%localhost:5046%'
    OR dicom_metadata::text ILIKE '%/uploads/%'
  )

UNION ALL
SELECT 'student_questions', COUNT(*)
FROM public.student_questions
WHERE custom_image_url ILIKE '%localhost%'

UNION ALL
SELECT 'quiz_questions', COUNT(*)
FROM public.quiz_questions
WHERE image_url ILIKE '%localhost%'

UNION ALL
SELECT 'flashcards', COUNT(*)
FROM public.flashcards
WHERE image_url ILIKE '%localhost%'

UNION ALL
SELECT 'users (avatar_url)', COUNT(*)
FROM public.users
WHERE avatar_url ILIKE '%localhost%';

-- 1b. Sample rows (Visual QA)
SELECT id, custom_image_url, updated_at
FROM public.visual_qa_sessions
WHERE custom_image_url ILIKE '%localhost%'
   OR custom_image_url LIKE '/uploads/%'
ORDER BY updated_at DESC NULLS LAST
LIMIT 20;

SELECT mi.id, mi.case_id, mi.image_url
FROM public.medical_images mi
WHERE mi.image_url ILIKE '%localhost%'
   OR mi.image_url LIKE '/uploads/%'
ORDER BY mi.created_at DESC NULLS LAST
LIMIT 20;

-- =============================================================================
-- SECTION 2B — Clear broken legacy URLs (files deleted from Storage)
-- =============================================================================
-- Run this block if you accidentally deleted objects from Storage.
-- Effect: no more localhost requests from FE; sessions need re-upload for images.

BEGIN;

-- 2B-a. visual_qa_sessions — nullable column
UPDATE public.visual_qa_sessions
SET custom_image_url = NULL
WHERE custom_image_url IS NOT NULL
  AND (
    custom_image_url ~* '^https?://localhost:[0-9]+/'
    OR custom_image_url LIKE '/uploads/%'
    OR custom_image_url ILIKE '%localhost:%'
  );

-- 2B-b. medical_images — NOT NULL column → empty string
UPDATE public.medical_images
SET image_url = ''
WHERE image_url ~* '^https?://localhost:[0-9]+/'
   OR image_url LIKE '/uploads/%'
   OR image_url ILIKE '%localhost:%';

-- 2B-c. case_media.media_url — NOT NULL → empty string
UPDATE public.case_media
SET media_url = ''
WHERE media_url ~* '^https?://localhost:[0-9]+/'
   OR media_url LIKE '/uploads/%'
   OR media_url ILIKE '%localhost:%';

-- 2B-d. case_media.storage_path — clear legacy paths
UPDATE public.case_media
SET storage_path = NULL
WHERE storage_path IS NOT NULL
  AND (
    storage_path ~* '^https?://localhost:[0-9]+/'
    OR storage_path LIKE '/uploads/%'
    OR storage_path ILIKE '%localhost:%'
  );

-- 2B-e. case_media.dicom_metadata (jsonb) — remove preview_url if localhost;
--       also strip localhost strings from JSON text when other keys reference them
UPDATE public.case_media
SET dicom_metadata = (
  CASE
    WHEN dicom_metadata ? 'preview_url'
         AND (
           dicom_metadata->>'preview_url' ~* '^https?://localhost:'
           OR dicom_metadata->>'preview_url' LIKE '/uploads/%'
         )
    THEN dicom_metadata - 'preview_url'
    ELSE dicom_metadata
  END
)
WHERE dicom_metadata IS NOT NULL
  AND dicom_metadata::text ILIKE '%localhost:%';

-- 2B-f. Any remaining localhost strings inside jsonb (other keys)
UPDATE public.case_media
SET dicom_metadata = replace(
    replace(
        replace(dicom_metadata::text,
            'https://localhost:5047/uploads/', ''),
        'http://localhost:5047/uploads/', ''),
    'http://localhost:5046/uploads/', '')::jsonb
WHERE dicom_metadata IS NOT NULL
  AND dicom_metadata::text ILIKE '%localhost:%';

-- 2B-g. student_questions
UPDATE public.student_questions
SET custom_image_url = NULL
WHERE custom_image_url IS NOT NULL
  AND (
    custom_image_url ~* '^https?://localhost:[0-9]+/'
    OR custom_image_url LIKE '/uploads/%'
    OR custom_image_url ILIKE '%localhost:%'
  );

-- 2B-h. quiz_questions
UPDATE public.quiz_questions
SET image_url = NULL
WHERE image_url IS NOT NULL
  AND (
    image_url ~* '^https?://localhost:[0-9]+/'
    OR image_url LIKE '/uploads/%'
    OR image_url ILIKE '%localhost:%'
  );

-- 2B-i. flashcards
UPDATE public.flashcards
SET image_url = NULL
WHERE image_url IS NOT NULL
  AND (
    image_url ~* '^https?://localhost:[0-9]+/'
    OR image_url LIKE '/uploads/%'
    OR image_url ILIKE '%localhost:%'
  );

-- 2B-j. users.avatar_url
UPDATE public.users
SET avatar_url = NULL
WHERE avatar_url IS NOT NULL
  AND (
    avatar_url ~* '^https?://localhost:[0-9]+/'
    OR avatar_url LIKE '/uploads/%'
    OR avatar_url ILIKE '%localhost:%'
  );

COMMIT;

-- =============================================================================
-- SECTION 2A — Rewrite URLs to Supabase (ONLY if files still exist in Storage)
-- =============================================================================
-- Do NOT run 2A and 2B together. Skip 2A if Storage objects were deleted.
-- Uncomment and run only after confirming objects exist in bucket medical-images.

/*
BEGIN;

UPDATE public.medical_images
SET image_url = CASE
    WHEN image_url ~* '^https?://localhost:[0-9]+/uploads/' THEN
        'https://jshryhplbayoymtthqpu.supabase.co/storage/v1/object/public/medical-images/'
        || regexp_replace(image_url, '^https?://localhost:[0-9]+/uploads/', '')
    WHEN image_url LIKE '/uploads/%' THEN
        'https://jshryhplbayoymtthqpu.supabase.co/storage/v1/object/public/medical-images/'
        || substring(image_url FROM length('/uploads/') + 1)
    ELSE image_url
END
WHERE image_url ~* '^https?://localhost:[0-9]+/uploads/'
   OR image_url LIKE '/uploads/%';

UPDATE public.visual_qa_sessions
SET custom_image_url = CASE
    WHEN custom_image_url ~* '^https?://localhost:[0-9]+/uploads/' THEN
        'https://jshryhplbayoymtthqpu.supabase.co/storage/v1/object/public/medical-images/'
        || regexp_replace(custom_image_url, '^https?://localhost:[0-9]+/uploads/', '')
    WHEN custom_image_url LIKE '/uploads/%' THEN
        'https://jshryhplbayoymtthqpu.supabase.co/storage/v1/object/public/medical-images/'
        || substring(custom_image_url FROM length('/uploads/') + 1)
    ELSE custom_image_url
END
WHERE custom_image_url ~* '^https?://localhost:[0-9]+/uploads/'
   OR custom_image_url LIKE '/uploads/%';

UPDATE public.case_media
SET media_url = CASE
    WHEN media_url ~* '^https?://localhost:[0-9]+/uploads/' THEN
        'https://jshryhplbayoymtthqpu.supabase.co/storage/v1/object/public/medical-images/'
        || regexp_replace(media_url, '^https?://localhost:[0-9]+/uploads/', '')
    WHEN media_url LIKE '/uploads/%' THEN
        'https://jshryhplbayoymtthqpu.supabase.co/storage/v1/object/public/medical-images/'
        || substring(media_url FROM length('/uploads/') + 1)
    ELSE media_url
END
WHERE media_url ~* '^https?://localhost:[0-9]+/uploads/'
   OR media_url LIKE '/uploads/%';

UPDATE public.case_media
SET storage_path = CASE
    WHEN storage_path ~* '^https?://localhost:[0-9]+/uploads/' THEN
        regexp_replace(storage_path, '^https?://localhost:[0-9]+/uploads/', '')
    WHEN storage_path LIKE '/uploads/%' THEN
        substring(storage_path FROM length('/uploads/') + 1)
    ELSE storage_path
END
WHERE storage_path ~* '^https?://localhost:[0-9]+/uploads/'
   OR storage_path LIKE '/uploads/%';

-- jsonb: cast to text for replace, cast back to jsonb
UPDATE public.case_media
SET dicom_metadata = replace(
    replace(
        replace(dicom_metadata::text,
            'https://localhost:5047/uploads/',
            'https://jshryhplbayoymtthqpu.supabase.co/storage/v1/object/public/medical-images/'),
        'http://localhost:5047/uploads/',
        'https://jshryhplbayoymtthqpu.supabase.co/storage/v1/object/public/medical-images/'),
    'http://localhost:5046/uploads/',
    'https://jshryhplbayoymtthqpu.supabase.co/storage/v1/object/public/medical-images/')::jsonb
WHERE dicom_metadata IS NOT NULL
  AND (
    dicom_metadata::text ILIKE '%localhost:5047%'
    OR dicom_metadata::text ILIKE '%localhost:5046%'
  );

COMMIT;
*/

-- =============================================================================
-- SECTION 3 — Post-migration verification
-- =============================================================================

SELECT COUNT(*) AS remaining_localhost_medical_images
FROM public.medical_images
WHERE image_url ILIKE '%localhost%';

SELECT COUNT(*) AS remaining_localhost_visual_qa
FROM public.visual_qa_sessions
WHERE custom_image_url ILIKE '%localhost%';

SELECT COUNT(*) AS remaining_localhost_dicom_metadata
FROM public.case_media
WHERE dicom_metadata IS NOT NULL
  AND dicom_metadata::text ILIKE '%localhost%';

-- Sessions cleared (expect custom_image_url IS NULL for broken rows)
SELECT id, custom_image_url, status, updated_at
FROM public.visual_qa_sessions
ORDER BY updated_at DESC NULLS LAST
LIMIT 15;

-- =============================================================================
-- NOTES
-- =============================================================================
-- 1. SECTION 2B does NOT restore deleted Storage files. After cleanup:
--    - Personal Visual QA: user uploads DICOM .zip/.rar again via upload-personal.
--    - Catalog cases: expert re-uploads images or re-runs ingest.
--
-- 2. Supabase Point-in-Time Recovery / backup may restore deleted Storage objects
--    if deletion was recent — contact Supabase support or check dashboard backups
--    before accepting permanent data loss.
--
-- 3. jsonb columns must use ::text for ILIKE / replace, then ::jsonb to write back.
--
-- 4. Run SECTION 1 → SECTION 2B → SECTION 3 in order for your current situation.
