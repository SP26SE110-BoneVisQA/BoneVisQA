-- Create password_reset_tokens table for forgot password feature
-- Run this SQL in your Supabase SQL Editor

CREATE TABLE IF NOT EXISTS password_reset_tokens (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token VARCHAR(255) NOT NULL UNIQUE,
    expires_at TIMESTAMP WITH TIME ZONE NOT NULL,
    is_used BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now()
);

-- Create index on token for fast lookup
CREATE INDEX IF NOT EXISTS idx_password_reset_tokens_token ON password_reset_tokens(token);

-- Create index on user_id for cleanup queries
CREATE INDEX IF NOT EXISTS idx_password_reset_tokens_user_id ON password_reset_tokens(user_id);

COMMENT ON TABLE password_reset_tokens IS 'Stores password reset tokens for forgot password feature';
COMMENT ON COLUMN password_reset_tokens.token IS 'Secure random token sent to user email';
COMMENT ON COLUMN password_reset_tokens.expires_at IS 'Token expiration time (default 1 hour)';
COMMENT ON COLUMN password_reset_tokens.is_used IS 'Whether the token has been used';
