-- =====================================================
-- DEBUG: Chi tiết điểm của một attempt cụ thể
-- 
-- Thay YOUR_ATTEMPT_ID bằng attempt_id cần kiểm tra
-- =====================================================
SELECT 
    qa.id as attempt_id,
    qa.score as stored_score,
    u.full_name as student_name,
    q.title as quiz_title,
    qq.id as question_id,
    CASE 
        WHEN qq.type = 0 THEN 'MultipleChoice'
        WHEN qq.type = 1 THEN 'TrueFalse'
        WHEN qq.type = 2 THEN 'MultiSelect'
        WHEN qq.type = 3 THEN 'Essay'
        WHEN qq.type = 4 THEN 'FillInBlank'
        ELSE 'Unknown'
    END as question_type,
    qq.type as type_code,
    qa2.student_answer,
    qa2.essay_answer,
    qa2.is_correct,
    qa2.score_awarded,
    qa2.is_graded
FROM quiz_attempts qa
JOIN quizzes q ON q.id = qa.quiz_id
JOIN users u ON u.id = qa.student_id
JOIN student_quiz_answers qa2 ON qa2.attempt_id = qa.id
JOIN quiz_questions qq ON qq.id = qa2.question_id
WHERE qa.id = '5870d77e-2c24-4126-9ff5-0f0e2acdd47c'  -- Thay bằng attempt_id của bạn
ORDER BY qq.id;
