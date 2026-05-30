-- Personal ingest ownership on medical_cases (Supabase / Postgres).
-- Run if not applying EF migration via dotnet ef database update.

BEGIN;

ALTER TABLE public.medical_cases
  ADD COLUMN IF NOT EXISTS owner_student_id uuid;

DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint WHERE conname = 'medical_cases_owner_student_id_fkey'
  ) THEN
    ALTER TABLE public.medical_cases
      ADD CONSTRAINT medical_cases_owner_student_id_fkey
      FOREIGN KEY (owner_student_id) REFERENCES public.users (id) ON DELETE SET NULL;
  END IF;
END $$;

CREATE INDEX IF NOT EXISTS idx_medical_cases_owner_student_id
  ON public.medical_cases (owner_student_id)
  WHERE owner_student_id IS NOT NULL;

COMMIT;
