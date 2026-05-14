-- ==============================================
-- ANALYTICS + QUIZ EXTENSIONS MIGRATION
-- Run this SQL script to create all required tables
-- ==============================================

-- ==============================================
-- PART 1: LEARNING ANALYTICS TABLES
-- ==============================================

-- Student Competencies Table
CREATE TABLE IF NOT EXISTS student_competencies (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    student_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    bone_specialty_id UUID REFERENCES bone_specialties(id) ON DELETE SET NULL,
    pathology_category_id UUID REFERENCES pathology_categories(id) ON DELETE SET NULL,
    score DECIMAL(5,2) DEFAULT 0,
    total_attempts INT DEFAULT 0,
    correct_attempts INT DEFAULT 0,
    mastery_level VARCHAR(50) DEFAULT 'Beginner', -- Beginner, Intermediate, Proficient, Expert
    last_attempt_at TIMESTAMP,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW(),
    UNIQUE(student_id, bone_specialty_id, pathology_category_id)
);

CREATE INDEX idx_student_competencies_student ON student_competencies(student_id);
CREATE INDEX idx_student_competencies_bone ON student_competencies(bone_specialty_id);
CREATE INDEX idx_student_competencies_pathology ON student_competencies(pathology_category_id);

-- Error Patterns Table
CREATE TABLE IF NOT EXISTS error_patterns (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    student_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    question_pattern TEXT, -- Pattern của câu hỏi sai (e.g., "Upper Limb", "Fracture", "X-ray interpretation")
    error_topic VARCHAR(256), -- Topic chính của lỗi
    error_count INT DEFAULT 1,
    topic_hint TEXT, -- Gợi ý AI phân tích
    first_occurred_at TIMESTAMP DEFAULT NOW(),
    last_occurred_at TIMESTAMP DEFAULT NOW(),
    is_resolved BOOLEAN DEFAULT FALSE,
    resolved_at TIMESTAMP,
    created_at TIMESTAMP DEFAULT NOW()
);

CREATE INDEX idx_error_patterns_student ON error_patterns(student_id);
CREATE INDEX idx_error_patterns_topic ON error_patterns(error_topic);
CREATE INDEX idx_error_patterns_resolved ON error_patterns(is_resolved);

-- Learning Insights Table
CREATE TABLE IF NOT EXISTS learning_insights (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    student_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    insight_type VARCHAR(50) NOT NULL, -- WeakTopic, Improvement, RecommendedAction, ErrorPattern
    title VARCHAR(256) NOT NULL,
    description TEXT,
    confidence DECIMAL(3,2) DEFAULT 0.5, -- 0.00 to 1.00
    related_bone_specialty_id UUID REFERENCES bone_specialties(id) ON DELETE SET NULL,
    related_pathology_id UUID REFERENCES pathology_categories(id) ON DELETE SET NULL,
    recommended_action TEXT,
    is_read BOOLEAN DEFAULT FALSE,
    is_action_taken BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP DEFAULT NOW()
);

CREATE INDEX idx_learning_insights_student ON learning_insights(student_id);
CREATE INDEX idx_learning_insights_type ON learning_insights(insight_type);
CREATE INDEX idx_learning_insights_read ON learning_insights(is_read);

-- ==============================================
-- PART 2: QUIZ EXTENSIONS TABLES
-- ==============================================

-- Add columns to quizzes table for adaptive/spaced repetition
ALTER TABLE quizzes ADD COLUMN IF NOT EXISTS adaptive_difficulty BOOLEAN DEFAULT FALSE;
ALTER TABLE quizzes ADD COLUMN IF NOT EXISTS spaced_repetition_enabled BOOLEAN DEFAULT FALSE;

-- Add columns to quiz_attempts table
ALTER TABLE quiz_attempts ADD COLUMN IF NOT EXISTS difficulty_level VARCHAR(20) DEFAULT 'Medium'; -- Easy, Medium, Hard
ALTER TABLE quiz_attempts ADD COLUMN IF NOT EXISTS next_review_date DATE;
ALTER TABLE quiz_attempts ADD COLUMN IF NOT EXISTS ease_factor DECIMAL(4,2) DEFAULT 2.5; -- SM-2 algorithm factor
ALTER TABLE quiz_attempts ADD COLUMN IF NOT EXISTS review_interval INT DEFAULT 1; -- Days until next review
ALTER TABLE quiz_attempts ADD COLUMN IF NOT EXISTS current_question_index INT DEFAULT 0;

-- Review Schedule Table (Spaced Repetition)
CREATE TABLE IF NOT EXISTS review_schedules (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    student_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    case_id UUID REFERENCES medical_cases(id) ON DELETE CASCADE,
    quiz_id UUID REFERENCES quizzes(id) ON DELETE CASCADE,
    question_id UUID REFERENCES quiz_questions(id) ON DELETE CASCADE,
    next_review_date DATE NOT NULL,
    ease_factor DECIMAL(4,2) DEFAULT 2.5,
    interval_days INT DEFAULT 1,
    repetition_count INT DEFAULT 0,
    last_review_date TIMESTAMP,
    last_quality INT DEFAULT -1, -- SM-2 quality rating 0-5
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW()
);

CREATE INDEX idx_review_schedules_student ON review_schedules(student_id);
CREATE INDEX idx_review_schedules_due ON review_schedules(next_review_date);
CREATE INDEX idx_review_schedules_case ON review_schedules(case_id);

-- Quiz Review Items Table (Detailed Review)
CREATE TABLE IF NOT EXISTS quiz_review_items (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    attempt_id UUID NOT NULL REFERENCES quiz_attempts(id) ON DELETE CASCADE,
    question_id UUID NOT NULL REFERENCES quiz_questions(id) ON DELETE CASCADE,
    question_text TEXT NOT NULL,
    student_answer TEXT,
    correct_answer TEXT,
    is_correct BOOLEAN,
    ai_explanation TEXT, -- AI giải thích chi tiết
    related_cases JSONB DEFAULT '[]', -- Array of related case IDs
    topic_tags JSONB DEFAULT '[]', -- Array of topic strings
    created_at TIMESTAMP DEFAULT NOW()
);

CREATE INDEX idx_quiz_review_items_attempt ON quiz_review_items(attempt_id);
CREATE INDEX idx_quiz_review_items_question ON quiz_review_items(question_id);

-- ==============================================
-- PART 3: COMPETENCY MASTERING THRESHOLDS (Reference Data)
-- ==============================================

CREATE TABLE IF NOT EXISTS competency_definitions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    bone_specialty_id UUID REFERENCES bone_specialties(id) ON DELETE CASCADE,
    pathology_category_id UUID REFERENCES pathology_categories(id) ON DELETE CASCADE,
    mastery_thresholds JSONB DEFAULT '{"Beginner": 0, "Intermediate": 40, "Proficient": 60, "Expert": 80}', -- Score thresholds
    description TEXT,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW(),
    UNIQUE(bone_specialty_id, pathology_category_id)
);

-- ==============================================
-- PART 4: SAMPLE DATA - Competency Definitions
-- ==============================================

INSERT INTO competency_definitions (bone_specialty_id, pathology_category_id, mastery_thresholds, description)
SELECT bs.id, pc.id, 
    '{"Beginner": 0, "Intermediate": 40, "Proficient": 60, "Expert": 80}',
    'Default mastery thresholds for ' || bs.name || ' - ' || pc.name
FROM bone_specialties bs
CROSS JOIN pathology_categories pc
WHERE bs.id IS NOT NULL AND pc.id IS NOT NULL
ON CONFLICT (bone_specialty_id, pathology_category_id) DO NOTHING;

-- ==============================================
-- VERIFICATION QUERIES
-- ==============================================

-- Check all tables created
-- SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_name IN (
--     'student_competencies', 'error_patterns', 'learning_insights', 
--     'review_schedules', 'quiz_review_items', 'competency_definitions'
-- );

-- Check columns added to existing tables
-- SELECT column_name, data_type FROM information_schema.columns WHERE table_name = 'quizzes' AND column_name IN ('adaptive_difficulty', 'spaced_repetition_enabled');
-- SELECT column_name, data_type FROM information_schema.columns WHERE table_name = 'quiz_attempts' AND column_name IN ('difficulty_level', 'next_review_date', 'ease_factor', 'review_interval', 'current_question_index');
