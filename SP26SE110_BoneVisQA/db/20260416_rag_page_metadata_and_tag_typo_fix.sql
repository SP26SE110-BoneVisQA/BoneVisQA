-- RAG page metadata + tag type typo normalization.
-- Safe to run multiple times.

ALTER TABLE IF EXISTS document_chunks
    ADD COLUMN IF NOT EXISTS start_page integer;

ALTER TABLE IF EXISTS document_chunks
    ADD COLUMN IF NOT EXISTS end_page integer;

ALTER TABLE IF EXISTS pending_document_chunks
    ADD COLUMN IF NOT EXISTS start_page integer;

ALTER TABLE IF EXISTS pending_document_chunks
    ADD COLUMN IF NOT EXISTS end_page integer;

UPDATE document_chunks
SET start_page = chunk_order + 1
WHERE start_page IS NULL OR start_page <= 0;

UPDATE document_chunks
SET end_page = start_page
WHERE end_page IS NULL OR end_page <= 0;

UPDATE pending_document_chunks
SET start_page = chunk_order + 1
WHERE start_page IS NULL OR start_page <= 0;

UPDATE pending_document_chunks
SET end_page = start_page
WHERE end_page IS NULL OR end_page <= 0;

ALTER TABLE document_chunks
    ALTER COLUMN start_page SET NOT NULL;

ALTER TABLE document_chunks
    ALTER COLUMN end_page SET NOT NULL;

ALTER TABLE pending_document_chunks
    ALTER COLUMN start_page SET NOT NULL;

ALTER TABLE pending_document_chunks
    ALTER COLUMN end_page SET NOT NULL;

-- Normalize historical typo in tag type.
UPDATE tags
SET type = 'Lesion'
WHERE lower(type) = 'lession';
