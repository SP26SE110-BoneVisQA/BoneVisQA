-- Persist last indexing failure reason for admin status API (survives reload).
ALTER TABLE documents
ADD COLUMN IF NOT EXISTS indexing_error_message text;
