-- Check ALL attempts for "Phân loại gãy xương theo hệ thống AO" quiz
SELECT 
    qa.id AS attempt_id,
    u.full_name AS student_name,
    q.title AS quiz_title,
    qa.score AS stored_score,
    qa.completed_at,
    qc.total_questions,
    ROUND(100.0 / qc.total_questions, 2) AS points_per_q,
    -- Count correct answers (only non-essay)
    COUNT(DISTINCT CASE 
        WHEN qq.type != 3 AND qa2.is_correct = true THEN qq.id 
    END) AS mc_correct,
    -- Count essay correct (has scoreAwarded > 0)
    COUNT(DISTINCT CASE 
        WHEN qq.type = 3 AND qa2.score_awarded > 0 THEN qq.id 
    END) AS essay_correct,
    -- Total essay count
    COUNT(DISTINCT CASE WHEN qq.type = 3 THEN qq.id END) AS total_essay,
    -- Calculate correct score
    ROUND(
        SUM(
            CASE
                WHEN qq.type = 3 THEN COALESCE(qa2.score_awarded, 0)
                WHEN qq.type IN (0, 1, 2, 4) AND qa2.is_correct = true THEN (100.0 / qc.total_questions)
                WHEN qq.type IN (0, 1, 2, 4) THEN 0
                ELSE 0
            END
        ), 1
    ) AS new_correct_score,
    -- Compare
    CASE 
        WHEN ABS(qa.score - ROUND(
            SUM(
                CASE
                    WHEN qq.type = 3 THEN COALESCE(qa2.score_awarded, 0)
                    WHEN qq.type IN (0, 1, 2, 4) AND qa2.is_correct = true THEN (100.0 / qc.total_questions)
                    ELSE 0
                END
            ), 1
        )) < 0.01 THEN 'OK'
        ELSE 'SAI'
    END AS status
FROM quiz_attempts qa
JOIN quizzes q ON q.id = qa.quiz_id
JOIN users u ON u.id = qa.student_id
JOIN quiz_questions qq ON qq.quiz_id = q.id
JOIN student_quiz_answers qa2 ON qa2.attempt_id = qa.id AND qa2.question_id = qq.id
JOIN (
    SELECT quiz_id, COUNT(*) AS total_questions 
    FROM quiz_questions 
    GROUP BY quiz_id
) qc ON qc.quiz_id = q.id
WHERE q.title = 'Phân loại gãy xương theo hệ thống AO'
AND qa.completed_at IS NOT NULL
GROUP BY qa.id, u.full_name, q.title, qa.score, qa.completed_at, qc.total_questions
ORDER BY qa.completed_at DESC;
