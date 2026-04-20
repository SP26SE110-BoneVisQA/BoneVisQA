ALTER TABLE qa_messages
ADD COLUMN IF NOT EXISTS client_request_id varchar(100);

CREATE UNIQUE INDEX IF NOT EXISTS ux_qa_messages_session_client_request_role
ON qa_messages (session_id, client_request_id, role)
WHERE client_request_id IS NOT NULL;
