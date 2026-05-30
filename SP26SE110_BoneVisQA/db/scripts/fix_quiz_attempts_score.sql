-- Fix quiz_attempts.score to be calculated from score_awarded instead of being manually set
-- This fixes the bug where stored_score didn't match calculated_score from scoreAwarded

UPDATE quiz_attempts qa
SET score = (
    SELECT 
        CASE 
            WHEN SUM(qq.max_score) = 0 THEN 0
            ELSE (SUM(sa.score_awarded)::float / SUM(qq.max_score) * 100)
        END
    FROM student_quiz_answers sa
    JOIN quiz_questions qq ON sa.question_id = qq.id
    WHERE sa.attempt_id = qa.id
)
WHERE EXISTS (
    SELECT 1 
    FROM student_quiz_answers sa
    JOIN quiz_questions qq ON sa.question_id = qq.id
    WHERE sa.attempt_id = qa.id
);

-- Verify the fix
SELECT 
    qa.id AS attempt_id,
    qa.score AS fixed_score,
    SUM(qq.max_score) AS total_max_score,
    SUM(CASE WHEN sa.score_awarded IS NOT NULL THEN sa.score_awarded ELSE 0 END) AS total_score_awarded,
    COUNT(sa.id) AS answer_count,
    SUM(CASE WHEN sa.is_correct = true THEN 1 ELSE 0 END) AS correct_count
FROM quiz_attempts qa
JOIN student_quiz_answers sa ON qa.id = sa.attempt_id
JOIN quiz_questions qq ON sa.question_id = qq.id
GROUP BY qa.id, qa.score
ORDER BY qa.id;
