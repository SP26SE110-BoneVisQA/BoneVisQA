-- =====================================================
-- FIX: Recalculate quiz attempt scores - CORRECT LOGIC
-- 
-- Logic đúng:
-- - Score = Điểm MC/TF/MultiSelect/FillInBlank đúng + Điểm Essay (scoreAwarded)
-- - correct_count = Chỉ đếm câu MC/TF/MultiSelect/FillInBlank đúng (KHÔNG đếm Essay)
-- - Essay: được điểm = scoreAwarded (có thể là 0, 25, 50, 75, 100 tùy lecturer chấm)
-- =====================================================

-- Xem trước kết quả
SELECT 
    qa.id as attempt_id,
    u.full_name as student_name,
    q.title as quiz_title,
    qa.score as old_score,
    qc.total_questions,
    ROUND(100.0 / qc.total_questions, 2) as points_per_q,
    -- correct_count: CHỈ đếm câu MC/TF/MultiSelect/FillInBlank đúng (type != 3)
    COUNT(DISTINCT CASE 
        WHEN qq.type != 3 AND qa2.is_correct = true THEN qq.id
        -- Essay KHÔNG đếm vào correct_count vì nó có thể được 0 điểm dù lecturer đã chấm
    END) as mc_correct_count,
    -- Essay count: đếm số essay có score_awarded > 0
    COUNT(DISTINCT CASE 
        WHEN qq.type = 3 AND qa2.score_awarded > 0 THEN qq.id
    END) as essay_correct_count,
    -- Tính điểm đúng:
    -- MC/TF/etc: points_per_q nếu is_correct = true, 0 nếu sai
    -- Essay: scoreAwarded (điểm thực tế lecturer chấm)
    ROUND(
        SUM(
            CASE
                -- Essay: điểm = scoreAwarded (có thể là 0, 25, 50, 75, 100...)
                WHEN qq.type = 3 THEN COALESCE(qa2.score_awarded, 0)
                -- MC/TF/MultiSelect/FillInBlank: điểm = points_per_q nếu đúng
                WHEN qq.type IN (0, 1, 2, 4) AND qa2.is_correct = true THEN (100.0 / qc.total_questions)
                -- MC/TF sai: 0 điểm
                WHEN qq.type IN (0, 1, 2, 4) THEN 0
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
    v_new_score NUMERIC;
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
        -- Tính điểm mới: MC đúng + Essay scoreAwarded
        SELECT ROUND(
            SUM(
                CASE
                    WHEN qq.type = 3 THEN COALESCE(qa2.score_awarded, 0)
                    WHEN qq.type IN (0, 1, 2, 4) AND qa2.is_correct = true THEN (100.0 / rec.total_questions)
                    WHEN qq.type IN (0, 1, 2, 4) THEN 0
                    ELSE 0
                END
            ), 1
        ) INTO v_new_score
        FROM student_quiz_answers qa2
        JOIN quiz_questions qq ON qq.id = qa2.question_id
        WHERE qa2.attempt_id = rec.attempt_id;
        
        UPDATE quiz_attempts 
        SET score = v_new_score
        WHERE id = rec.attempt_id;
        
        RAISE NOTICE 'Fixed attempt: % - new score: %', rec.attempt_id, v_new_score;
    END LOOP;
    
    RAISE NOTICE 'All quiz attempt scores have been recalculated!';
END $$;
