-- BoneVisQA Supabase sync script for current BE contract
-- Safe-oriented script: uses IF EXISTS / IF NOT EXISTS where possible.
-- Apply on Supabase SQL editor after taking a backup/snapshot.

BEGIN;

-- Required extensions used by the current EF/Core model.
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";
CREATE EXTENSION IF NOT EXISTS "vector";

-- =========================================================
-- 1. Documents semantic versioning sync
-- =========================================================

ALTER TABLE public.documents
  ADD COLUMN IF NOT EXISTS updated_at timestamp with time zone,
  ADD COLUMN IF NOT EXISTS pending_target_version character varying(32);

UPDATE public.documents
SET updated_at = COALESCE(updated_at, created_at, now())
WHERE updated_at IS NULL;

ALTER TABLE public.documents
  ALTER COLUMN updated_at SET DEFAULT now();

ALTER TABLE public.documents
  ALTER COLUMN version TYPE character varying(32);

UPDATE public.documents
SET version = CASE
  WHEN version IS NULL OR btrim(version::text) = '' THEN '1.0.0'
  WHEN version::text ~ '^[0-9]+$' AND version::int <= 1 THEN '1.0.0'
  WHEN version::text ~ '^[0-9]+$' THEN '1.' || GREATEST(version::int - 1, 0)::text || '.0'
  ELSE btrim(version::text)
END;

ALTER TABLE public.documents
  ALTER COLUMN version SET DEFAULT '1.0.0',
  ALTER COLUMN version SET NOT NULL;

-- =========================================================
-- 2. Medical cases semantic versioning sync
-- =========================================================

ALTER TABLE public.medical_cases
  ALTER COLUMN version TYPE character varying(32);

UPDATE public.medical_cases
SET version = CASE
  WHEN version IS NULL OR btrim(version::text) = '' THEN '1.0.0'
  WHEN version::text ~ '^[0-9]+$' AND version::int <= 1 THEN '1.0.0'
  WHEN version::text ~ '^[0-9]+$' THEN '1.' || GREATEST(version::int - 1, 0)::text || '.0'
  ELSE btrim(version::text)
END
WHERE version IS NULL
   OR btrim(version::text) = ''
   OR version::text ~ '^[0-9]+$';

ALTER TABLE public.medical_cases
  ALTER COLUMN version SET DEFAULT '1.0.0',
  ALTER COLUMN version SET NOT NULL;

-- =========================================================
-- 3. Visual QA idempotency support
-- =========================================================

ALTER TABLE public.qa_messages
  ADD COLUMN IF NOT EXISTS client_request_id character varying(100),
  ADD COLUMN IF NOT EXISTS citations_json jsonb;

ALTER TABLE public.visual_qa_sessions
  ADD COLUMN IF NOT EXISTS requested_review_message_id uuid;

CREATE UNIQUE INDEX IF NOT EXISTS ux_qa_messages_session_client_request_role
ON public.qa_messages (session_id, client_request_id, role)
WHERE client_request_id IS NOT NULL;

-- Performance indexes used by the current model/query patterns.
CREATE INDEX IF NOT EXISTS idx_qa_messages_role
ON public.qa_messages (role);

CREATE INDEX IF NOT EXISTS idx_qa_messages_session_created_at
ON public.qa_messages (session_id, created_at);

CREATE INDEX IF NOT EXISTS idx_visual_qa_sessions_student
ON public.visual_qa_sessions (student_id);

CREATE INDEX IF NOT EXISTS idx_visual_qa_sessions_case
ON public.visual_qa_sessions (case_id);

CREATE INDEX IF NOT EXISTS idx_visual_qa_sessions_status
ON public.visual_qa_sessions (status);

CREATE INDEX IF NOT EXISTS idx_visual_qa_sessions_requested_review_message
ON public.visual_qa_sessions (requested_review_message_id);

CREATE UNIQUE INDEX IF NOT EXISTS pending_document_chunks_doc_id_chunk_order_key
ON public.pending_document_chunks (doc_id, chunk_order);

-- =========================================================
-- 4. Column defaults / lengths aligned with BE model
-- =========================================================

ALTER TABLE public.qa_messages
  ALTER COLUMN role TYPE character varying(20);

ALTER TABLE public.visual_qa_sessions
  ALTER COLUMN status TYPE character varying(40),
  ALTER COLUMN status SET DEFAULT 'Active';

ALTER TABLE public.documents
  ALTER COLUMN pending_target_version TYPE character varying(32);

-- =========================================================
-- 5. ROI + notifications (Visual QA thread + FE deep links)
--    See also db/20260418_contract_fe_be_alignment_readme.sql
-- =========================================================

ALTER TABLE public.qa_messages
  ADD COLUMN IF NOT EXISTS coordinates jsonb;

ALTER TABLE public.notifications
  ADD COLUMN IF NOT EXISTS target_url text;

COMMIT;
