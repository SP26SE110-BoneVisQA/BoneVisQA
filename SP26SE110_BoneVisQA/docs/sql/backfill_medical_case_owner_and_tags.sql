-- =============================================================================
-- BoneVisQA — Backfill medical_cases owner + Location/Lesion tags
-- =============================================================================
-- Purpose:
--   Fix legacy catalog cases that appear in student library but not on expert
--   /cases, or show "Unknown location/lesion" / missing expert name in UI.
--
-- What it does (catalog cases only: owner_student_id IS NULL):
--   1. Backfill created_by_expert_id from validated_by, promote session, assigned expert
--   2. Backfill validated_by from created_by_expert_id when missing
--   3. Ensure Location + Lesion Type tags from category (or case_metadata fallback)
--   4. Ensure "Student Q&A" Source tag on cases promoted from Visual QA
--
-- Run in: Supabase Dashboard → SQL Editor (or psql)
-- Safe to re-run: uses NOT EXISTS / ON CONFLICT DO NOTHING
-- Date: 2026-06-08
--
-- NOTE: Production DB may enforce tags_name_unique (unique on name only), NOT
-- (name, type). Section 3 reuses existing tag rows by name — never inserts a
-- duplicate name such as "Trauma & Fractures" with a second type.
-- =============================================================================

-- ─── Optional: set a fallback expert when no owner can be inferred ───────────
-- Uncomment and replace UUID if you still have orphan cases after STEP 2.
-- \set fallback_expert_id '00000000-0000-0000-0000-000000000000'

BEGIN;

-- =============================================================================
-- SECTION 1 — Diagnostics (inspect before/after; does not mutate)
-- =============================================================================

-- 1a. Catalog cases missing owner
SELECT
    mc.id,
    mc.title,
    mc.created_by_expert_id,
    mc.validated_by,
    mc.assigned_expert_id,
    mc.category_id,
    c.name AS category_name,
    mc.is_approved,
    mc.created_at
FROM public.medical_cases mc
LEFT JOIN public.categories c ON c.id = mc.category_id
WHERE mc.owner_student_id IS NULL
  AND mc.created_by_expert_id IS NULL
  AND mc.validated_by IS NULL
ORDER BY mc.created_at DESC NULLS LAST;

-- 1b. Catalog cases missing Location tag
SELECT
    mc.id,
    mc.title,
    c.name AS category_name,
    cm.anatomy_site
FROM public.medical_cases mc
LEFT JOIN public.categories c ON c.id = mc.category_id
LEFT JOIN public.case_metadata cm ON cm.case_id = mc.id
WHERE mc.owner_student_id IS NULL
  AND NOT EXISTS (
      SELECT 1
      FROM public.case_tags ct
      JOIN public.tags t ON t.id = ct.tag_id
      WHERE ct.case_id = mc.id
        AND t.type IN ('Location', 'BoneLocation')
  )
ORDER BY mc.created_at DESC NULLS LAST;

-- 1c. Catalog cases missing Lesion Type tag
SELECT
    mc.id,
    mc.title,
    c.name AS category_name,
    cm.pathology_group
FROM public.medical_cases mc
LEFT JOIN public.categories c ON c.id = mc.category_id
LEFT JOIN public.case_metadata cm ON cm.case_id = mc.id
WHERE mc.owner_student_id IS NULL
  AND NOT EXISTS (
      SELECT 1
      FROM public.case_tags ct
      JOIN public.tags t ON t.id = ct.tag_id
      WHERE ct.case_id = mc.id
        AND t.type IN ('Lesion Type', 'Lesion')
  )
ORDER BY mc.created_at DESC NULLS LAST;

-- =============================================================================
-- SECTION 2 — Backfill expert ownership
-- =============================================================================

-- 2a. validated_by → created_by_expert_id
UPDATE public.medical_cases mc
SET
    created_by_expert_id = mc.validated_by,
    updated_at = NOW()
WHERE mc.owner_student_id IS NULL
  AND mc.created_by_expert_id IS NULL
  AND mc.validated_by IS NOT NULL;

-- 2b. Promoted Visual QA session → expert who approved/promoted
UPDATE public.medical_cases mc
SET
    created_by_expert_id = src.expert_user_id,
    validated_by = COALESCE(mc.validated_by, src.expert_user_id),
    validated_at = COALESCE(mc.validated_at, src.promoted_at, NOW()),
    updated_at = NOW()
FROM (
    SELECT DISTINCT ON (vqs.promoted_case_id)
        vqs.promoted_case_id AS case_id,
        COALESCE(vqs.expert_id, er.expert_id) AS expert_user_id,
        COALESCE(vqs.updated_at, er.created_at, vqs.created_at) AS promoted_at
    FROM public.visual_qa_sessions vqs
    LEFT JOIN public.expert_reviews er
        ON er.session_id = vqs.id
       AND (er.action IS NULL OR er.action ILIKE 'approve%')
    WHERE vqs.promoted_case_id IS NOT NULL
      AND COALESCE(vqs.expert_id, er.expert_id) IS NOT NULL
    ORDER BY vqs.promoted_case_id, vqs.updated_at DESC NULLS LAST, vqs.created_at DESC
) src
WHERE mc.id = src.case_id
  AND mc.owner_student_id IS NULL
  AND mc.created_by_expert_id IS NULL;

-- 2c. assigned_expert_id fallback
UPDATE public.medical_cases mc
SET
    created_by_expert_id = mc.assigned_expert_id,
    validated_by = COALESCE(mc.validated_by, mc.assigned_expert_id),
    validated_at = COALESCE(mc.validated_at, mc.updated_at, mc.created_at, NOW()),
    updated_at = NOW()
WHERE mc.owner_student_id IS NULL
  AND mc.created_by_expert_id IS NULL
  AND mc.assigned_expert_id IS NOT NULL;

-- 2d. created_by_expert_id → validated_by (expert-created cases without validator)
UPDATE public.medical_cases mc
SET
    validated_by = mc.created_by_expert_id,
    validated_at = COALESCE(mc.validated_at, mc.created_at, NOW()),
    updated_at = NOW()
WHERE mc.owner_student_id IS NULL
  AND mc.validated_by IS NULL
  AND mc.created_by_expert_id IS NOT NULL;

-- 2e. OPTIONAL manual fallback — assign one known expert to remaining orphans
-- Replace UUID below with your demo expert user id, then uncomment.
/*
UPDATE public.medical_cases mc
SET
    created_by_expert_id = '00000000-0000-0000-0000-000000000000'::uuid,
    validated_by = COALESCE(mc.validated_by, '00000000-0000-0000-0000-000000000000'::uuid),
    validated_at = COALESCE(mc.validated_at, NOW()),
    updated_at = NOW()
WHERE mc.owner_student_id IS NULL
  AND mc.is_approved IS TRUE
  AND mc.is_active IS TRUE
  AND mc.created_by_expert_id IS NULL;
*/

-- =============================================================================
-- SECTION 3 — Ensure tags exist (reuse by name; tags_name_unique safe)
-- =============================================================================

-- 3a. Normalize existing category-name tags → Location (so BE location filter works)
UPDATE public.tags t
SET
    type = 'Location',
    updated_at = NOW()
FROM public.categories c
WHERE t.name = TRIM(c.name)
  AND NULLIF(TRIM(c.name), '') IS NOT NULL
  AND t.type NOT IN ('Location', 'BoneLocation', 'Source');

-- 3b. Insert category-name tags only when name does not exist yet
INSERT INTO public.tags (id, name, type, created_at, updated_at)
SELECT
    gen_random_uuid(),
    TRIM(c.name),
    'Location',
    NOW(),
    NOW()
FROM public.categories c
WHERE NULLIF(TRIM(c.name), '') IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM public.tags t WHERE t.name = TRIM(c.name)
  )
ON CONFLICT (name) DO NOTHING;

-- 3c. anatomy_site tags (skip insert if name already taken)
INSERT INTO public.tags (id, name, type, created_at, updated_at)
SELECT
    gen_random_uuid(),
    TRIM(cm.anatomy_site),
    'Location',
    NOW(),
    NOW()
FROM (
    SELECT DISTINCT TRIM(anatomy_site) AS anatomy_site
    FROM public.case_metadata
    WHERE NULLIF(TRIM(anatomy_site), '') IS NOT NULL
) cm
WHERE NOT EXISTS (
    SELECT 1 FROM public.tags t WHERE t.name = cm.anatomy_site
)
ON CONFLICT (name) DO NOTHING;

-- 3d. pathology_group tags — reuse name if exists; else insert as Lesion Type
INSERT INTO public.tags (id, name, type, created_at, updated_at)
SELECT
    gen_random_uuid(),
    pg.pathology_group,
    'Lesion Type',
    NOW(),
    NOW()
FROM (
    SELECT DISTINCT TRIM(pathology_group) AS pathology_group
    FROM public.case_metadata
    WHERE NULLIF(TRIM(pathology_group), '') IS NOT NULL
) pg
WHERE NOT EXISTS (
    SELECT 1 FROM public.tags t WHERE t.name = pg.pathology_group
)
ON CONFLICT (name) DO NOTHING;

-- 3e. If pathology_group name already exists as Location/Custom, keep it (BE falls back to category/metadata)

-- 3f. Source tag for student-promoted library cases
INSERT INTO public.tags (id, name, type, created_at, updated_at)
SELECT gen_random_uuid(), 'Student Q&A', 'Source', NOW(), NOW()
WHERE NOT EXISTS (
    SELECT 1 FROM public.tags t WHERE t.name = 'Student Q&A'
)
ON CONFLICT (name) DO NOTHING;

-- =============================================================================
-- SECTION 4 — Link case_tags (category name → existing tag by name)
-- =============================================================================

INSERT INTO public.case_tags (case_id, tag_id, created_at)
SELECT
    mc.id,
    t.id,
    NOW()
FROM public.medical_cases mc
JOIN public.categories c ON c.id = mc.category_id
JOIN public.tags t ON t.name = TRIM(c.name)
WHERE mc.owner_student_id IS NULL
  AND NULLIF(TRIM(c.name), '') IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM public.case_tags ct
      JOIN public.tags tg ON tg.id = ct.tag_id
      WHERE ct.case_id = mc.id
        AND tg.type IN ('Location', 'BoneLocation')
  )
ON CONFLICT (case_id, tag_id) DO NOTHING;

-- =============================================================================
-- SECTION 5 — Link case_tags (lesion: same name tag OR pathology_group tag)
-- =============================================================================

INSERT INTO public.case_tags (case_id, tag_id, created_at)
SELECT
    mc.id,
    t.id,
    NOW()
FROM public.medical_cases mc
JOIN public.categories c ON c.id = mc.category_id
JOIN public.tags t ON t.name = TRIM(c.name)
WHERE mc.owner_student_id IS NULL
  AND NULLIF(TRIM(c.name), '') IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM public.case_tags ct
      JOIN public.tags tg ON tg.id = ct.tag_id
      WHERE ct.case_id = mc.id
        AND tg.type IN ('Lesion Type', 'Lesion')
  )
  AND NOT EXISTS (
      SELECT 1 FROM public.case_tags ct WHERE ct.case_id = mc.id AND ct.tag_id = t.id
  )
ON CONFLICT (case_id, tag_id) DO NOTHING;

-- =============================================================================
-- SECTION 6 — Link case_tags (Location from case_metadata.anatomy_site fallback)
-- =============================================================================

INSERT INTO public.case_tags (case_id, tag_id, created_at)
SELECT
    mc.id,
    t.id,
    NOW()
FROM public.medical_cases mc
JOIN public.case_metadata cm ON cm.case_id = mc.id
JOIN public.tags t ON t.name = TRIM(cm.anatomy_site)
WHERE mc.owner_student_id IS NULL
  AND NULLIF(TRIM(cm.anatomy_site), '') IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM public.case_tags ct
      JOIN public.tags tg ON tg.id = ct.tag_id
      WHERE ct.case_id = mc.id
        AND tg.type IN ('Location', 'BoneLocation')
  )
ON CONFLICT (case_id, tag_id) DO NOTHING;

-- =============================================================================
-- SECTION 7 — Link case_tags (Lesion from case_metadata.pathology_group fallback)
-- =============================================================================

INSERT INTO public.case_tags (case_id, tag_id, created_at)
SELECT
    mc.id,
    t.id,
    NOW()
FROM public.medical_cases mc
JOIN public.case_metadata cm ON cm.case_id = mc.id
JOIN public.tags t ON t.name = TRIM(cm.pathology_group)
WHERE mc.owner_student_id IS NULL
  AND NULLIF(TRIM(cm.pathology_group), '') IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM public.case_tags ct
      JOIN public.tags tg ON tg.id = ct.tag_id
      WHERE ct.case_id = mc.id
        AND tg.type IN ('Lesion Type', 'Lesion')
  )
  AND NOT EXISTS (
      SELECT 1 FROM public.case_tags ct WHERE ct.case_id = mc.id AND ct.tag_id = t.id
  )
ON CONFLICT (case_id, tag_id) DO NOTHING;

-- =============================================================================
-- SECTION 8 — Link "Student Q&A" Source tag on promoted cases
-- =============================================================================

INSERT INTO public.case_tags (case_id, tag_id, created_at)
SELECT
    mc.id,
    t.id,
    NOW()
FROM public.medical_cases mc
JOIN public.visual_qa_sessions vqs ON vqs.promoted_case_id = mc.id
JOIN public.tags t
  ON t.name = 'Student Q&A'
WHERE mc.owner_student_id IS NULL
  AND NOT EXISTS (
      SELECT 1
      FROM public.case_tags ct
      JOIN public.tags tg ON tg.id = ct.tag_id
      WHERE ct.case_id = mc.id
        AND tg.name = 'Student Q&A'
  )
ON CONFLICT (case_id, tag_id) DO NOTHING;

-- =============================================================================
-- SECTION 9 — Post-check summary
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
SELECT 'missing_lesion_tag', COUNT(*)
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

-- Review SECTION 9 output, then choose ONE:
--   COMMIT;   -- apply changes
--   ROLLBACK; -- dry-run (default — no data changed if you stop here)

-- COMMIT;
ROLLBACK;
