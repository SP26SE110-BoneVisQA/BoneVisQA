-- Semantic versioning for documents (Major.Minor.Patch), pending target version, and updated_at.
-- Run after backup. Safe to run once.

ALTER TABLE documents ADD COLUMN IF NOT EXISTS updated_at timestamp with time zone;
UPDATE documents SET updated_at = COALESCE(created_at, now()) WHERE updated_at IS NULL;

ALTER TABLE documents ADD COLUMN IF NOT EXISTS pending_target_version character varying(32);

-- Migrate integer version → semantic string
ALTER TABLE documents ADD COLUMN IF NOT EXISTS version_new character varying(32);
UPDATE documents
SET version_new = CASE
    WHEN version::text ~ '^[0-9]+$' AND (version::int) <= 1 THEN '1.0.0'
    WHEN version::text ~ '^[0-9]+$' THEN '1.' || GREATEST((version::int) - 1, 0)::text || '.0'
    ELSE COALESCE(NULLIF(trim(version::text), ''), '1.0.0')
END
WHERE version_new IS NULL;

-- If "version" is already text in some environments, handle gracefully:
UPDATE documents SET version_new = '1.0.0' WHERE version_new IS NULL;

ALTER TABLE documents DROP COLUMN IF EXISTS version;
ALTER TABLE documents RENAME COLUMN version_new TO version;
ALTER TABLE documents ALTER COLUMN version SET DEFAULT '1.0.0';
ALTER TABLE documents ALTER COLUMN version SET NOT NULL;

ALTER TABLE documents ALTER COLUMN updated_at SET DEFAULT now();
