ALTER TABLE visual_qa_sessions
ADD COLUMN IF NOT EXISTS requested_review_message_id uuid;

ALTER TABLE qa_messages
ADD COLUMN IF NOT EXISTS citations_json jsonb;

CREATE INDEX IF NOT EXISTS idx_visual_qa_sessions_requested_review_message
ON visual_qa_sessions (requested_review_message_id);
