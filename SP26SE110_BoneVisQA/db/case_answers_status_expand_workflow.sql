-- Run on Supabase (PostgreSQL) to align with CaseAnswerStatuses / expert workflow.
-- Drops and recreates the check constraint to allow new workflow values.

ALTER TABLE case_answers DROP CONSTRAINT IF EXISTS case_answers_status_check;

ALTER TABLE case_answers ADD CONSTRAINT case_answers_status_check CHECK (
    status = ANY (ARRAY[
        'Pending'::text,
        'RequiresLecturerReview'::text,
        'Approved'::text,
        'Edited'::text,
        'Rejected'::text,
        'Escalated'::text,
        'EscalatedToExpert'::text,
        'ExpertApproved'::text,
        'Revised'::text
    ])
);

-- Optional: migrate existing rows to new canonical labels (review before running).
-- UPDATE case_answers SET status = 'EscalatedToExpert' WHERE status = 'Escalated';
-- UPDATE case_answers SET status = 'ExpertApproved' WHERE status IN ('Approved', 'Revised') AND reviewed_by_id IS NOT NULL AND escalated_at IS NOT NULL;
