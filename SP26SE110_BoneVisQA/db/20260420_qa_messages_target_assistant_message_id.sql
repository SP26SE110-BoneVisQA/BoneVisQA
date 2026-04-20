-- Supabase / PostgreSQL: Visual QA feedback linked to assistant turn
-- Safe to run multiple times.

ALTER TABLE qa_messages
    ADD COLUMN IF NOT EXISTS target_assistant_message_id uuid NULL;

COMMENT ON COLUMN qa_messages.target_assistant_message_id IS
    'Assistant message id this Lecturer/Expert row responds to (Visual QA review thread).';
