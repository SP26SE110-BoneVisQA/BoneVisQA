-- BoneVisQA production schema hardening (run on Supabase after backup).
-- Complements EF migrations + db/20260516_case_metadata_ontology_medical_cases_audit.sql

BEGIN;

-- ---------------------------------------------------------------------------
-- 1. Performance indexes for multimodal RAG + Visual QA (safe IF NOT EXISTS)
-- ---------------------------------------------------------------------------

CREATE INDEX IF NOT EXISTS idx_medical_cases_is_approved_active
  ON public.medical_cases (is_approved, is_active)
  WHERE is_approved = TRUE AND is_active = TRUE;

CREATE INDEX IF NOT EXISTS idx_case_metadata_modality_anatomy
  ON public.case_metadata (modality, anatomy, pathology_group);

CREATE INDEX IF NOT EXISTS idx_case_text_embeddings_case_id
  ON public.case_text_embeddings (case_id);

CREATE INDEX IF NOT EXISTS idx_case_media_embeddings_media_id
  ON public.case_media_embeddings (media_id);

-- ---------------------------------------------------------------------------
-- 2. Optional FK constraints (skip if orphan rows exist — inspect first)
-- ---------------------------------------------------------------------------

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'announcements_assignment_id_fkey') THEN
    IF NOT EXISTS (
      SELECT 1 FROM public.announcements a
      WHERE a.assignment_id IS NOT NULL
        AND NOT EXISTS (SELECT 1 FROM public.class_cases cc WHERE cc.case_id = a.assignment_id)
    ) THEN
      -- assignment_id may reference class_cases or future assignment table; add only when semantics are confirmed.
      NULL;
    END IF;
  END IF;
END $$;

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'class_cases_announcement_id_fkey') THEN
    ALTER TABLE public.class_cases
      ADD CONSTRAINT class_cases_announcement_id_fkey
      FOREIGN KEY (announcement_id) REFERENCES public.announcements (id) ON DELETE SET NULL;
  END IF;
EXCEPTION
  WHEN foreign_key_violation THEN
    RAISE NOTICE 'Skipped class_cases_announcement_id_fkey: orphan announcement_id rows exist.';
END $$;

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'class_quiz_sessions_released_by_id_fkey') THEN
    ALTER TABLE public.class_quiz_sessions
      ADD CONSTRAINT class_quiz_sessions_released_by_id_fkey
      FOREIGN KEY (released_by_id) REFERENCES public.users (id) ON DELETE SET NULL;
  END IF;
EXCEPTION
  WHEN foreign_key_violation THEN
    RAISE NOTICE 'Skipped class_quiz_sessions_released_by_id_fkey: orphan released_by_id rows exist.';
END $$;

COMMIT;

-- ---------------------------------------------------------------------------
-- 3. DROP / cleanup (run ONLY after team confirms — NOT in transaction above)
-- ---------------------------------------------------------------------------
-- DO NOT drop __EFMigrationsHistory if you still use `dotnet ef database update`.
-- DO NOT drop medical_images until FE fully uses case_media previews only.
--
-- Example: remove legacy local-upload rows (adjust predicate before running):
-- DELETE FROM public.medical_cases
-- WHERE is_approved = FALSE
--   AND description = '(no diagnosis)'
--   AND title LIKE 'Ingested case %'
--   AND NOT EXISTS (SELECT 1 FROM public.visual_qa_sessions v WHERE v.case_id = medical_cases.id);
--
-- DROP TABLE IF EXISTS public.__EFMigrationsHistory;  -- only if schema is 100% manual via SQL scripts
