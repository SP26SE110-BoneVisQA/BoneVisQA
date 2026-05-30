-- ============================================================================
-- Fix: Complete quiz_mode column type change to INTEGER
-- Values are already converted to '1' (string), now just need to change the column type
-- ============================================================================

-- Step 1: Drop the string default value
ALTER TABLE quizzes ALTER COLUMN quiz_mode DROP DEFAULT;
ALTER TABLE class_quiz_sessions ALTER COLUMN quiz_mode DROP DEFAULT;

-- Step 2: Alter column types to INTEGER
ALTER TABLE quizzes ALTER COLUMN quiz_mode TYPE INTEGER USING quiz_mode::integer;
ALTER TABLE class_quiz_sessions ALTER COLUMN quiz_mode TYPE INTEGER USING quiz_mode::integer;

-- Step 3: Set integer default values
ALTER TABLE quizzes ALTER COLUMN quiz_mode SET DEFAULT 1;
ALTER TABLE class_quiz_sessions ALTER COLUMN quiz_mode SET DEFAULT 1;

-- Step 4: Verify the changes
SELECT column_name, data_type, column_default
FROM information_schema.columns
WHERE table_name IN ('quizzes', 'class_quiz_sessions')
AND column_name = 'quiz_mode';

-- Step 5: Check unique values after fix
SELECT 'quizzes' as table_name, quiz_mode FROM quizzes GROUP BY quiz_mode
UNION ALL
SELECT 'class_quiz_sessions', quiz_mode FROM class_quiz_sessions GROUP BY quiz_mode;
