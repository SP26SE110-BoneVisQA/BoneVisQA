-- Document chunk metadata + document defaults for RAG citations (idempotent).
-- Apply via: dotnet ef database update OR run manually on Supabase.

ALTER TABLE documents
    ADD COLUMN IF NOT EXISTS default_modality text;

ALTER TABLE documents
    ADD COLUMN IF NOT EXISTS default_pathology_group text;

ALTER TABLE document_chunks
    ADD COLUMN IF NOT EXISTS modality text NOT NULL DEFAULT 'Other';

ALTER TABLE document_chunks
    ADD COLUMN IF NOT EXISTS anatomy text NOT NULL DEFAULT 'Other';

ALTER TABLE document_chunks
    ADD COLUMN IF NOT EXISTS pathology_group text NOT NULL DEFAULT 'Other';

CREATE INDEX IF NOT EXISTS ix_document_chunks_modality_anatomy
    ON document_chunks (modality, anatomy);

-- Optional: set default modality for existing documents before backfill
-- UPDATE documents SET default_modality = 'X-Ray' WHERE default_modality IS NULL;
