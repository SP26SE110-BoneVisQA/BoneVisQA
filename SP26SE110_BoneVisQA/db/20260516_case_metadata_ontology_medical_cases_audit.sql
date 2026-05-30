-- Ontology columns for multimodal RAG (public.case_metadata) + promotion audit on medical_cases.
-- Apply on the same Postgres used by BoneVisQA.API and BoneVisQA.AI.

BEGIN;

ALTER TABLE public.case_metadata
  ADD COLUMN IF NOT EXISTS anatomy_site text,
  ADD COLUMN IF NOT EXISTS laterality text,
  ADD COLUMN IF NOT EXISTS view_position text,
  ADD COLUMN IF NOT EXISTS difficulty text,
  ADD COLUMN IF NOT EXISTS source_type text,
  ADD COLUMN IF NOT EXISTS quality_score double precision;

-- Normalize legacy pathology label to ontology vocabulary.
UPDATE public.case_metadata
SET pathology_group = 'Infection'
WHERE pathology_group IN ('Inflammation', 'inflammation');

ALTER TABLE public.medical_cases
  ADD COLUMN IF NOT EXISTS review_version character varying(32),
  ADD COLUMN IF NOT EXISTS validated_by uuid,
  ADD COLUMN IF NOT EXISTS validated_at timestamp with time zone;

-- FK matches EF migration medical_cases_validated_by_fkey (skip if already present)
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint WHERE conname = 'medical_cases_validated_by_fkey'
  ) THEN
    ALTER TABLE public.medical_cases
      ADD CONSTRAINT medical_cases_validated_by_fkey
      FOREIGN KEY (validated_by) REFERENCES public.users (id) ON DELETE SET NULL;
  END IF;
END $$;

CREATE INDEX IF NOT EXISTS "IX_medical_cases_validated_by"
  ON public.medical_cases (validated_by);

UPDATE public.medical_cases
SET review_version = COALESCE(NULLIF(btrim(review_version::text), ''), version::text, '1.0.0')
WHERE review_version IS NULL;

COMMIT;
