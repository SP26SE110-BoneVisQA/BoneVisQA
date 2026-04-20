ALTER TABLE expert_reviews
ADD COLUMN IF NOT EXISTS corrected_roi jsonb NULL;
