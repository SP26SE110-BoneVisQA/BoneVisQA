-- Roll back Visual QA sessions that were expert-approved but never promoted to the case library.
-- Symptom: status = ExpertApproved, promoted_case_id IS NULL (partial success from approve-before-promote).
-- After BE promote validation fix, re-run promote or approve-and-promote from the expert UI.

-- Preview stuck rows
SELECT
  vqs.id,
  vqs.student_id,
  vqs.status,
  vqs.study_mode,
  vqs.case_id,
  vqs.promoted_case_id,
  vqs.expert_id,
  vqs.updated_at
FROM public.visual_qa_sessions vqs
WHERE vqs.status = 'ExpertApproved'
  AND vqs.promoted_case_id IS NULL
  AND EXISTS (
    SELECT 1
    FROM public.qa_messages m
    WHERE m.session_id = vqs.id
  )
ORDER BY vqs.updated_at DESC;

-- Single session rollback (replace UUID)
/*
BEGIN;
UPDATE public.visual_qa_sessions
SET
  status = 'EscalatedToExpert',
  updated_at = NOW() AT TIME ZONE 'utc'
WHERE id = '8aaf1164-0000-0000-0000-000000000000'::uuid
  AND status = 'ExpertApproved'
  AND promoted_case_id IS NULL;
COMMIT;
*/

-- Batch rollback all stuck expert-approved sessions without a promoted case
/*
BEGIN;
UPDATE public.visual_qa_sessions
SET
  status = 'EscalatedToExpert',
  updated_at = NOW() AT TIME ZONE 'utc'
WHERE status = 'ExpertApproved'
  AND promoted_case_id IS NULL
  AND EXISTS (
    SELECT 1 FROM public.qa_messages m WHERE m.session_id = visual_qa_sessions.id
  );
COMMIT;
*/
