-- Fix: Add missing view_count column to medical_cases table
-- Run this SQL on your PostgreSQL database

ALTER TABLE medical_cases
ADD COLUMN IF NOT EXISTS view_count integer NOT NULL DEFAULT 0;

-- Create index for better query performance
CREATE INDEX IF NOT EXISTS IX_medical_cases_view_count ON medical_cases(view_count);
