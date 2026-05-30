-- =============================================
-- FIX: Chuyển quiz_questions.type từ varchar sang integer
-- =============================================

ALTER TABLE quiz_questions 
ALTER COLUMN type TYPE INTEGER 
USING CASE WHEN type IS NULL THEN NULL ELSE CAST(type AS INTEGER) END;

-- =============================================
-- ĐÁNH DẤU MIGRATION ĐÃ ÁP DỤNG
-- =============================================
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260516102011_FixQuizQuestionType', '8.0.0'
WHERE NOT EXISTS (
    SELECT 1 FROM "__EFMigrationsHistory" 
    WHERE "MigrationId" = '20260516102011_FixQuizQuestionType'
);

-- =============================================
-- XÁC NHẬN THAY ĐỔI
-- =============================================
SELECT column_name, data_type, udt_name
FROM information_schema.columns 
WHERE table_name = 'quiz_questions' AND column_name = 'type';
