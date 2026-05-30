-- ==============================================
-- 3 NEW TABLES FOR BONE VIS QA
-- Copy and run these SQL statements manually
-- ==============================================

-- ==============================================
-- TABLE 1: question_trends (Topic Reports)
-- ==============================================
CREATE TABLE IF NOT EXISTS question_trends (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    topic_id UUID NOT NULL,
    topic_type VARCHAR(20) NOT NULL, -- 'bone_specialty' or 'pathology'
    question_count INT DEFAULT 0,
    trend_direction VARCHAR(10) DEFAULT 'stable', -- 'up', 'down', 'stable'
    change_percentage DECIMAL(5,2) DEFAULT 0,
    period_start DATE,
    period_end DATE,
    created_at TIMESTAMP DEFAULT NOW()
);

CREATE INDEX idx_question_trends_topic ON question_trends(topic_id, topic_type);

-- ==============================================
-- TABLE 2: flashcard_decks (Flashcard)
-- ==============================================
CREATE TABLE IF NOT EXISTS flashcard_decks (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    deck_name VARCHAR(255) NOT NULL,
    description TEXT,
    student_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    card_count INT DEFAULT 0,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW()
);

CREATE INDEX idx_flashcard_decks_student ON flashcard_decks(student_id);

-- ==============================================
-- TABLE 3: flashcards (Flashcard)
-- ==============================================
CREATE TABLE IF NOT EXISTS flashcards (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    deck_id UUID NOT NULL REFERENCES flashcard_decks(id) ON DELETE CASCADE,
    front_content TEXT NOT NULL,
    back_content TEXT NOT NULL,
    image_url VARCHAR(500),
    ease_factor DECIMAL(4,2) DEFAULT 2.5, -- SM-2 algorithm
    interval_days INT DEFAULT 1,
    repetition_count INT DEFAULT 0,
    next_review_date DATE,
    last_review_date TIMESTAMP,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW()
);

CREATE INDEX idx_flashcards_deck ON flashcards(deck_id);
CREATE INDEX idx_flashcards_review ON flashcards(next_review_date);

-- Composite indexes for common query patterns
CREATE INDEX idx_flashcards_deck_review ON flashcards(deck_id, next_review_date);
CREATE INDEX idx_flashcards_last_review ON flashcards(last_review_date);
