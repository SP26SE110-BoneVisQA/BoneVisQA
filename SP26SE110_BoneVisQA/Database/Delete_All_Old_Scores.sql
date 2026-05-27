-- ==============================================
-- XÓA TOÀN BỘ ĐIỂM CŨ - QUIZ ATTEMPTS
-- ==============================================

-- 1. Xóa quiz_review_items (chi tiết review - phụ thuộc quiz_attempts)
DELETE FROM quiz_review_items;

-- 2. Xóa review_schedules (lịch ôn tập - phụ thuộc quiz_attempts)
DELETE FROM review_schedules;

-- 3. Xóa student_competencies (điểm năng lực)
DELETE FROM student_competencies;

-- 4. Xóa error_patterns (mẫu lỗi)
DELETE FROM error_patterns;

-- 5. Xóa learning_insights (insights học tập)
DELETE FROM learning_insights;

-- 6. Xóa quiz_attempts (điểm thi chính)
DELETE FROM quiz_attempts;

-- 7. Reset sequences
ALTER SEQUENCE IF EXISTS quiz_review_items_id_seq RESTART WITH 1;
ALTER SEQUENCE IF EXISTS review_schedules_id_seq RESTART WITH 1;
ALTER SEQUENCE IF EXISTS student_competencies_id_seq RESTART WITH 1;
ALTER SEQUENCE IF EXISTS error_patterns_id_seq RESTART WITH 1;
ALTER SEQUENCE IF EXISTS learning_insights_id_seq RESTART WITH 1;
ALTER SEQUENCE IF EXISTS quiz_attempts_id_seq RESTART WITH 1;

-- Verify
SELECT 'Da xoa thanh cong!' AS status;
SELECT COUNT(*) AS remaining_attempts FROM quiz_attempts;
