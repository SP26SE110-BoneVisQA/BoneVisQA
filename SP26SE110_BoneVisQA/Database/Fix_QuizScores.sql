-- =====================================================
-- FIX: Recalculate and UPDATE quiz attempt scores
-- 
-- Logic: 
-- - Total quiz score = 100, chia đều cho các câu
-- - MC/TF: được điểm nếu is_correct = true
-- - Essay: được điểm bằng score_awarded (> 0 mới là đúng)
-- - MultiSelect/FillInBlank: được điểm nếu is_correct = true
-- =====================================================

-- Xem trước kết quả
SELECT 
    qa.id as attempt_id,
    u.full_name as student_name,
    q.title as quiz_title,
    qa.score as old_score,
    qc.total_questions,
    ROUND(100.0 / qc.total_questions, 2) as points_per_q,
    COUNT(DISTINCT CASE 
        WHEN qq.type = 3 AND qa2.score_awarded > 0 THEN qq.id
        WHEN qq.type != 3 AND qa2.is_correct = true THEN qq.id
    END) as correct_count,
    ROUND(
        SUM(
            CASE
                -- Essay: điểm = score_awarded
                WHEN qq.type = 3 THEN COALESCE(qa2.score_awarded, 0)
                -- MC/TF/MultiSelect/FillInBlank: điểm = points_per_q nếu đúng
                WHEN qq.type IN (0, 1, 2, 4) THEN 
                    CASE WHEN qa2.is_correct = true THEN (100.0 / qc.total_questions) ELSE 0 END
                ELSE 0
            END
        ), 1
    ) as new_score
FROM quiz_attempts qa
JOIN quizzes q ON q.id = qa.quiz_id
JOIN users u ON u.id = qa.student_id
JOIN quiz_questions qq ON qq.quiz_id = q.id
JOIN student_quiz_answers qa2 ON qa2.attempt_id = qa.id AND qa2.question_id = qq.id
JOIN (
    SELECT quiz_id, COUNT(*) as total_questions 
    FROM quiz_questions 
    GROUP BY quiz_id
) qc ON qc.quiz_id = q.id
WHERE qa.completed_at IS NOT NULL
GROUP BY qa.id, u.full_name, q.title, qa.score, qc.total_questions
ORDER BY qa.completed_at DESC
LIMIT 50;

-- =====================================================
-- UPDATE: Chạy query này để fix scores
-- =====================================================
DO $$
DECLARE
    rec RECORD;
BEGIN
    FOR rec IN 
        SELECT DISTINCT qa.id as attempt_id, q.id as quiz_id, qc.total_questions
        FROM quiz_attempts qa
        JOIN quizzes q ON q.id = qa.quiz_id
        JOIN (
            SELECT quiz_id, COUNT(*) as total_questions 
            FROM quiz_questions 
            GROUP BY quiz_id
        ) qc ON qc.quiz_id = q.id
        WHERE qa.completed_at IS NOT NULL
    LOOP
        -- Tính điểm mới
        UPDATE quiz_attempts 
        SET score = (
            SELECT ROUND(
                SUM(
                    CASE
                        WHEN qq.type = 3 THEN COALESCE(qa2.score_awarded, 0)
                        WHEN qq.type IN (0, 1, 2, 4) THEN 
                            CASE WHEN qa2.is_correct = true THEN (100.0 / rec.total_questions) ELSE 0 END
                        ELSE 0
                    END
                ), 1
            )
            FROM student_quiz_answers qa2
            JOIN quiz_questions qq ON qq.id = qa2.question_id
            WHERE qa2.attempt_id = rec.attempt_id
        )
        WHERE id = rec.attempt_id;
        
        RAISE NOTICE 'Fixed attempt: %', rec.attempt_id;
    END LOOP;
    
    RAISE NOTICE 'All quiz attempt scores have been recalculated!';
END $$;
