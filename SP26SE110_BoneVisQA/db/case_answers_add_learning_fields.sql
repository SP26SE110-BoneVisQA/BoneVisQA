-- Run on Supabase (PostgreSQL) if columns are not present yet.
-- Aligns with SEPS: structured explanations including key imaging signs and reflective questions.

ALTER TABLE case_answers
    ADD COLUMN IF NOT EXISTS key_imaging_findings text;

ALTER TABLE case_answers
    ADD COLUMN IF NOT EXISTS reflective_questions text;
