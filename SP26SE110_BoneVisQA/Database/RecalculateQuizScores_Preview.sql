-- =====================================================
-- Preview: Xem trước điểm cũ và điểm mới
-- Logic: Tổng điểm = 100, chia đều cho các câu
-- =====================================================
WITH question_counts AS (
    SELECT quiz_id, COUNT(*) as total_questions
    FROM quiz_questions
    GROUP BY quiz_id
),
answer_summary AS (
    SELECT 
        qa2.attempt_id,
        qc.total_questions,
        CASE 
            WHEN qc.total_questions > 0 THEN 100.0 / qc.total_questions 
            ELSE 0 
        END as points_per_question,
        SUM(
            CASE 
                WHEN (qq.type = 3 AND qa2.score_awarded > 0) THEN 1
                WHEN (qq.type != 3 AND qa2.is_correct = true) THEN 1
                ELSE 0
            END
        ) as correct_count
    FROM student_quiz_answers qa2
    JOIN quiz_questions qq ON qq.id = qa2.question_id
    JOIN quizzes q ON q.id = qq.quiz_id
    JOIN question_counts qc ON qc.quiz_id = q.id
    GROUP BY qa2.attempt_id, qc.total_questions
)
SELECT 
    qa.id as attempt_id,
    u.full_name as student_name,
    q.title as quiz_title,
    qc.total_questions,
    ROUND(100.0 / qc.total_questions, 2) as points_per_q,
    qa.score as old_score,
    ac.correct_count,
    ROUND(ac.correct_count * ac.points_per_question, 1) as new_score
FROM quiz_attempts qa
JOIN quizzes q ON q.id = qa.quiz_id
JOIN users u ON u.id = qa.student_id
JOIN question_counts qc ON qc.quiz_id = q.id
JOIN answer_summary ac ON ac.attempt_id = qa.id
WHERE qa.completed_at IS NOT NULL
ORDER BY qa.completed_at DESC
LIMIT 20;
