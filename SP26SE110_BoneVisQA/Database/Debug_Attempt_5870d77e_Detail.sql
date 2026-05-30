-- =====================================================
-- DEBUG: Chi tiết điểm của attempt 5870d77e-2c24-4126-9ff5-0f0e2acdd47c
-- Xem từng câu hỏi và điểm
-- =====================================================
SELECT 
    qa.id as attempt_id,
    u.full_name as student_name,
    q.title as quiz_title,
    qa.score as stored_score,
    qq.id as question_id,
    qq.question_text as question,
    CASE 
        WHEN qq.type = 0 THEN 'MultipleChoice'
        WHEN qq.type = 1 THEN 'TrueFalse'
        WHEN qq.type = 2 THEN 'MultiSelect'
        WHEN qq.type = 3 THEN 'Essay'
        WHEN qq.type = 4 THEN 'FillInBlank'
        ELSE 'Unknown'
    END as question_type,
    qq.type as type_code,
    qq.correct_answer,
    qq.correct_answers,
    qq.accepted_answers,
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
WHERE qa.id = '5870d77e-2c24-4126-9ff5-0f0e2acdd47c'
ORDER BY qq.id;

-- =====================================================
-- TÍNH LẠI ĐIỂM ĐÚNG
-- =====================================================
WITH attempt_data AS (
    SELECT 
        qq.id as question_id,
        qq.type as question_type,
        qq.correct_answer,
        qa2.student_answer,
        qa2.is_correct,
        qa2.score_awarded
    FROM quiz_attempts qa
    JOIN student_quiz_answers qa2 ON qa2.attempt_id = qa.id
    JOIN quiz_questions qq ON qq.id = qa2.question_id
    WHERE qa.id = '5870d77e-2c24-4126-9ff5-0f0e2acdd47c'
),
total_calc AS (
    SELECT 
        COUNT(*) as total_questions,
        100.0 / COUNT(*) as points_per_q
    FROM quiz_questions
    WHERE quiz_id = (SELECT quiz_id FROM quiz_attempts WHERE id = '5870d77e-2c24-4126-9ff5-0f0e2acdd47c')
)
SELECT 
    '=== CHI TIẾT TỪNG CÂU ===' as info,
    NULL as question_id
UNION ALL
SELECT 
    CASE 
        WHEN type = 3 THEN 'Essay: scoreAwarded = ' || COALESCE(score_awarded::text, 'NULL')
        WHEN is_correct = true THEN 'MC/TF đúng: +' || (SELECT ROUND(points_per_q, 2) FROM total_calc) || ' điểm'
        ELSE 'MC/TF sai: +0 điểm'
    END as info,
    question_id::text
FROM attempt_data
ORDER BY question_id NULLS LAST;

-- =====================================================
-- TÍNH TỔNG ĐIỂM ĐÚNG
-- =====================================================
WITH attempt_data AS (
    SELECT 
        qq.type as question_type,
        qa2.is_correct,
        qa2.score_awarded
    FROM quiz_attempts qa
    JOIN student_quiz_answers qa2 ON qa2.attempt_id = qa.id
    JOIN quiz_questions qq ON qq.id = qa2.question_id
    WHERE qa.id = '5870d77e-2c24-4126-9ff5-0f0e2acdd47c'
),
total_calc AS (
    SELECT 
        COUNT(*) as total_questions,
        100.0 / COUNT(*) as points_per_q
    FROM quiz_questions
    WHERE quiz_id = (SELECT quiz_id FROM quiz_attempts WHERE id = '5870d77e-2c24-4126-9ff5-0f0e2acdd47c')
),
score_calc AS (
    SELECT 
        SUM(
            CASE
                -- Essay: điểm = scoreAwarded (có thể là 0)
                WHEN type = 3 THEN COALESCE(score_awarded, 0)
                -- MC/TF đúng: + pointsPerQuestion
                WHEN is_correct = true THEN (SELECT points_per_q FROM total_calc)
                -- MC/TF sai: 0
                ELSE 0
            END
        ) as correct_score
    FROM attempt_data
)
SELECT 
    'ĐIỂM ĐÚNG:' as label,
    ROUND(correct_score, 1) as value
FROM score_calc
UNION ALL
SELECT 
    'ĐIỂM LƯU TRONG DB:' as label,
    qa.score as value
FROM quiz_attempts qa
WHERE qa.id = '5870d77e-2c24-4126-9ff5-0f0e2acdd47c';
