-- =============================================
-- MIGRATION SCRIPT: Quiz Features Enhancement
-- Date: 2026-05-15
-- Features: Hint, Explanation, QuizMode, ShuffleOptions, MultiSelect, FillInBlank
-- =============================================

-- =============================================
-- STEP 1: Add new columns to quiz_questions
-- =============================================

-- Add hint column for practice mode hints
ALTER TABLE quiz_questions 
ADD COLUMN IF NOT EXISTS hint TEXT;

-- Add explanation column for answer explanations
ALTER TABLE quiz_questions 
ADD COLUMN IF NOT EXISTS explanation TEXT;

-- Add correct_answers JSONB for multi-select questions (e.g., ["A", "C"])
ALTER TABLE quiz_questions 
ADD COLUMN IF NOT EXISTS correct_answers JSONB;

-- Add accepted_answers JSONB for fill-in-blank questions (case-insensitive)
ALTER TABLE quiz_questions 
ADD COLUMN IF NOT EXISTS accepted_answers JSONB;

-- =============================================
-- STEP 2: Add new columns to quizzes
-- =============================================

-- Add quiz_mode column (exam, practice, adaptive)
ALTER TABLE quizzes 
ADD COLUMN IF NOT EXISTS quiz_mode INTEGER DEFAULT 1;

-- =============================================
-- STEP 3: Add new columns to class_quiz_sessions
-- =============================================

-- Add shuffle_options column
ALTER TABLE class_quiz_sessions 
ADD COLUMN IF NOT EXISTS shuffle_options BOOLEAN DEFAULT FALSE;

-- Add quiz_mode column to class_quiz_sessions
ALTER TABLE class_quiz_sessions 
ADD COLUMN IF NOT EXISTS quiz_mode INTEGER DEFAULT 1;

-- =============================================
-- STEP 4: Create ENUM for quiz_mode (if not exists)
-- =============================================

DO $$BEGIN
    CREATE TYPE quiz_mode_enum AS ENUM ('Exam', 'Practice', 'Adaptive');
EXCEPTION
    WHEN duplicate_object THEN null;
END $$;

-- =============================================
-- STEP 5: Update comments/descriptions
-- =============================================

COMMENT ON COLUMN quiz_questions.hint IS 'Hint for the question - only shown in practice mode';
COMMENT ON COLUMN quiz_questions.explanation IS 'Explanation of the correct answer - shown after submission';
COMMENT ON COLUMN quiz_questions.correct_answers IS 'JSON array of correct answers for multi-select questions, e.g., ["A", "C"]';
COMMENT ON COLUMN quiz_questions.accepted_answers IS 'JSON array of accepted answers for fill-in-blank questions (case-insensitive)';
COMMENT ON COLUMN quizzes.quiz_mode IS 'Quiz mode: 1=Exam, 2=Practice, 3=Adaptive';
COMMENT ON COLUMN class_quiz_sessions.shuffle_options IS 'Shuffle the order of answer options A, B, C, D for each student';
COMMENT ON COLUMN class_quiz_sessions.quiz_mode IS 'Quiz mode: 1=Exam, 2=Practice, 3=Adaptive';

-- =============================================
-- STEP 6: Update existing quiz_sessions to use exam mode (1) as default
-- =============================================

UPDATE quizzes SET quiz_mode = 1 WHERE quiz_mode IS NULL;
UPDATE class_quiz_sessions SET quiz_mode = 1 WHERE quiz_mode IS NULL;

-- =============================================
-- STEP 7: Create indexes (optional but recommended)
-- =============================================

CREATE INDEX IF NOT EXISTS idx_quiz_questions_hint ON quiz_questions(hint) WHERE hint IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_quizzes_quiz_mode ON quizzes(quiz_mode);
CREATE INDEX IF NOT EXISTS idx_class_quiz_sessions_quiz_mode ON class_quiz_sessions(quiz_mode);

-- =============================================
-- ROLLBACK SCRIPT (if needed)
-- =============================================
-- ALTER TABLE quiz_questions DROP COLUMN IF EXISTS hint;
-- ALTER TABLE quiz_questions DROP COLUMN IF EXISTS explanation;
-- ALTER TABLE quiz_questions DROP COLUMN IF EXISTS correct_answers;
-- ALTER TABLE quiz_questions DROP COLUMN IF EXISTS accepted_answers;
-- ALTER TABLE quizzes DROP COLUMN IF EXISTS quiz_mode;
-- ALTER TABLE class_quiz_sessions DROP COLUMN IF EXISTS shuffle_options;
-- ALTER TABLE class_quiz_sessions DROP COLUMN IF EXISTS quiz_mode;
-- DROP INDEX IF EXISTS idx_quiz_questions_hint;
-- DROP INDEX IF EXISTS idx_quizzes_quiz_mode;
-- DROP INDEX IF EXISTS idx_class_quiz_sessions_quiz_mode;

PRINT 'Migration completed successfully!';
