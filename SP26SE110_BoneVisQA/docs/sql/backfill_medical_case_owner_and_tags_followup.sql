-- =============================================================================
-- BoneVisQA — Follow-up backfill (after backfill_medical_case_owner_and_tags.sql)
-- =============================================================================
-- Use when SECTION 9 still shows:
--   missing_owner > 0        → assign demo expert (SECTION 2)
--   missing_location_tag > 0 → link General / metadata tag (SECTION 3)
--   missing_lesion_tag high  → EXPECTED with tags_name_unique; BE uses category
--                              fallback (SECTION 4 = diagnostic only)
--
-- IMPORTANT: If main script ended with ROLLBACK, nothing was saved — re-run main
-- script with COMMIT first, then run this file.
-- =============================================================================

BEGIN;

-- =============================================================================
-- SECTION 1 — List remaining problem rows (read-only inside txn)
-- =============================================================================

-- 1a. Orphan catalog cases (no owner)
SELECT
    mc.id,
    mc.title,
    mc.category_id,
    c.name AS category_name,
    mc.is_approved,
    mc.created_at,
    vqs.id AS promote_session_id,
    vqs.expert_id AS session_expert_id
FROM public.medical_cases mc
LEFT JOIN public.categories c ON c.id = mc.category_id
LEFT JOIN public.visual_qa_sessions vqs ON vqs.promoted_case_id = mc.id
WHERE mc.owner_student_id IS NULL
  AND mc.is_approved IS TRUE
  AND mc.is_active IS TRUE
  AND mc.created_by_expert_id IS NULL
ORDER BY mc.created_at DESC;

-- 1b. Missing Location-type tag (only 2 rows typically)
SELECT
    mc.id,
    mc.title,
    c.name AS category_name,
    cm.anatomy_site,
    cm.pathology_group
FROM public.medical_cases mc
LEFT JOIN public.categories c ON c.id = mc.category_id
LEFT JOIN public.case_metadata cm ON cm.case_id = mc.id
WHERE mc.owner_student_id IS NULL
  AND mc.is_approved IS TRUE
  AND mc.is_active IS TRUE
  AND NOT EXISTS (
      SELECT 1
      FROM public.case_tags ct
      JOIN public.tags t ON t.id = ct.tag_id
      WHERE ct.case_id = mc.id
        AND t.type IN ('Location', 'BoneLocation')
  );

-- =============================================================================
-- SECTION 2 — Assign first Expert user to orphan catalog cases
-- =============================================================================
-- Picks the earliest-created user with role Expert. Change subquery if you need
-- a specific expert email.

UPDATE public.medical_cases mc
SET
    created_by_expert_id = e.expert_id,
    validated_by = COALESCE(mc.validated_by, e.expert_id),
    validated_at = COALESCE(mc.validated_at, NOW()),
    updated_at = NOW()
FROM (
    SELECT u.id AS expert_id
    FROM public.users u
    INNER JOIN public.user_roles ur ON ur.user_id = u.id
    INNER JOIN public.roles r ON r.id = ur.role_id
    WHERE r.name ILIKE 'Expert'
    ORDER BY u.created_at NULLS LAST, u.id
    LIMIT 1
) e
WHERE mc.owner_student_id IS NULL
  AND mc.is_approved IS TRUE
  AND mc.is_active IS TRUE
  AND mc.created_by_expert_id IS NULL;

-- If SECTION 1a listed rows but count still > 0, no Expert user exists in DB.
-- Run this diagnostic:
-- SELECT u.id, u.full_name, u.email, r.name
-- FROM public.users u
-- JOIN public.user_roles ur ON ur.user_id = u.id
-- JOIN public.roles r ON r.id = ur.role_id
-- WHERE r.name ILIKE '%expert%';

-- =============================================================================
-- SECTION 3 — Fix missing Location tag (General fallback + metadata name)
-- =============================================================================

INSERT INTO public.tags (id, name, type, created_at, updated_at)
SELECT gen_random_uuid(), 'General', 'Location', NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM public.tags t WHERE t.name = 'General')
ON CONFLICT (name) DO NOTHING;

UPDATE public.tags
SET type = 'Location', updated_at = NOW()
WHERE name = 'General'
  AND type NOT IN ('Location', 'BoneLocation', 'Source');

-- 3a. Link category-name tag when category exists
INSERT INTO public.case_tags (case_id, tag_id, created_at)
SELECT mc.id, t.id, NOW()
FROM public.medical_cases mc
JOIN public.categories c ON c.id = mc.category_id
JOIN public.tags t ON t.name = TRIM(c.name)
WHERE mc.owner_student_id IS NULL
  AND mc.is_approved IS TRUE
  AND NOT EXISTS (
      SELECT 1
      FROM public.case_tags ct
      JOIN public.tags tg ON tg.id = ct.tag_id
      WHERE ct.case_id = mc.id
        AND tg.type IN ('Location', 'BoneLocation')
  )
ON CONFLICT (case_id, tag_id) DO NOTHING;

-- 3b. Link anatomy_site tag
INSERT INTO public.case_tags (case_id, tag_id, created_at)
SELECT mc.id, t.id, NOW()
FROM public.medical_cases mc
JOIN public.case_metadata cm ON cm.case_id = mc.id
JOIN public.tags t ON t.name = TRIM(cm.anatomy_site)
WHERE mc.owner_student_id IS NULL
  AND mc.is_approved IS TRUE
  AND NULLIF(TRIM(cm.anatomy_site), '') IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM public.case_tags ct
      JOIN public.tags tg ON tg.id = ct.tag_id
      WHERE ct.case_id = mc.id
        AND tg.type IN ('Location', 'BoneLocation')
  )
ON CONFLICT (case_id, tag_id) DO NOTHING;

-- 3c. Last resort: General
INSERT INTO public.case_tags (case_id, tag_id, created_at)
SELECT mc.id, t.id, NOW()
FROM public.medical_cases mc
CROSS JOIN public.tags t
WHERE t.name = 'General'
  AND mc.owner_student_id IS NULL
  AND mc.is_approved IS TRUE
  AND NOT EXISTS (
      SELECT 1
      FROM public.case_tags ct
      JOIN public.tags tg ON tg.id = ct.tag_id
      WHERE ct.case_id = mc.id
        AND tg.type IN ('Location', 'BoneLocation')
  )
ON CONFLICT (case_id, tag_id) DO NOTHING;

-- =============================================================================
-- SECTION 4 — Lesion tag check (informational — high count is OK)
-- =============================================================================
-- Production has tags_name_unique: one name = one tag, usually type Location.
-- Student UI (after BE deploy) uses category_name / pathology_group fallback
-- when no Lesion Type tag exists.

SELECT 'strict_lesion_tag_missing' AS metric, COUNT(*) AS cnt
FROM public.medical_cases mc
WHERE mc.owner_student_id IS NULL
  AND mc.is_approved IS TRUE
  AND mc.is_active IS TRUE
  AND NOT EXISTS (
      SELECT 1
      FROM public.case_tags ct
      JOIN public.tags t ON t.id = ct.tag_id
      WHERE ct.case_id = mc.id
        AND t.type IN ('Lesion Type', 'Lesion')
  );

SELECT 'ui_ok_lesion_via_category_or_tag' AS metric, COUNT(*) AS cnt
FROM public.medical_cases mc
WHERE mc.owner_student_id IS NULL
  AND mc.is_approved IS TRUE
  AND mc.is_active IS TRUE
  AND (
      mc.category_id IS NOT NULL
      OR EXISTS (
          SELECT 1 FROM public.case_metadata cm
          WHERE cm.case_id = mc.id
            AND NULLIF(TRIM(cm.pathology_group), '') IS NOT NULL
      )
      OR EXISTS (
          SELECT 1 FROM public.case_tags ct
          JOIN public.tags t ON t.id = ct.tag_id
          WHERE ct.case_id = mc.id
      )
  );

-- =============================================================================
-- SECTION 5 — Final summary (target: missing_owner=0, missing_location_tag=0)
-- =============================================================================

SELECT 'missing_owner' AS check_name, COUNT(*) AS remaining
FROM public.medical_cases mc
WHERE mc.owner_student_id IS NULL
  AND mc.is_approved IS TRUE
  AND mc.is_active IS TRUE
  AND mc.created_by_expert_id IS NULL

UNION ALL
SELECT 'missing_location_tag', COUNT(*)
FROM public.medical_cases mc
WHERE mc.owner_student_id IS NULL
  AND mc.is_approved IS TRUE
  AND mc.is_active IS TRUE
  AND NOT EXISTS (
      SELECT 1
      FROM public.case_tags ct
      JOIN public.tags t ON t.id = ct.tag_id
      WHERE ct.case_id = mc.id
        AND t.type IN ('Location', 'BoneLocation')
  )

UNION ALL
SELECT 'missing_lesion_tag_strict', COUNT(*)
FROM public.medical_cases mc
WHERE mc.owner_student_id IS NULL
  AND mc.is_approved IS TRUE
  AND mc.is_active IS TRUE
  AND NOT EXISTS (
      SELECT 1
      FROM public.case_tags ct
      JOIN public.tags t ON t.id = ct.tag_id
      WHERE ct.case_id = mc.id
        AND t.type IN ('Lesion Type', 'Lesion')
  );

-- Expected after follow-up:
--   missing_owner = 0
--   missing_location_tag = 0
--   missing_lesion_tag_strict may stay > 0 — OK for demo if BE + category set

COMMIT;
-- ROLLBACK;
