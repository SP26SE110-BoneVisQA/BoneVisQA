-- =====================================================
-- UPDATE: Recalculate quiz attempt scores
-- Logic mới: Tổng điểm = 100, chia đều cho các câu
-- Dựa trên is_correct (đúng = 1 điểm/câu, sai = 0)
-- Chạy sau khi xem preview
-- =====================================================
DO $$
DECLARE
    rec RECORD;
    v_total_questions INTEGER;
    v_points_per_question NUMERIC;
    v_correct_count INTEGER;
    v_new_score NUMERIC;
BEGIN
    FOR rec IN 
        SELECT DISTINCT qa.id as attempt_id, q.id as quiz_id
        FROM quiz_attempts qa
        JOIN quizzes q ON q.id = qa.quiz_id
        WHERE qa.completed_at IS NOT NULL
    LOOP
        -- Đếm số câu hỏi trong quiz
        SELECT COUNT(*) INTO v_total_questions
        FROM quiz_questions
        WHERE quiz_id = rec.quiz_id;
        
        -- Tính điểm/câu = 100 / tổng_câu
        IF v_total_questions > 0 THEN
            v_points_per_question := 100.0 / v_total_questions;
        ELSE
            v_points_per_question := 0;
        END IF;
        
        -- Đếm số câu đúng (không tính essay chưa chấm hoặc essay được 0 điểm)
        SELECT COUNT(*) INTO v_correct_count
        FROM student_quiz_answers qa2
        JOIN quiz_questions qq ON qq.id = qa2.question_id
        WHERE qa2.attempt_id = rec.attempt_id
        AND (
            (qq.type = 3 AND qa2.score_awarded > 0)  -- Essay được > 0 điểm (ít nhất 1 phần điểm)
            OR (qq.type != 3 AND qa2.is_correct = true)        -- MC/TF đúng
        );
        
        -- Tính điểm mới = số câu đúng × điểm/câu
        v_new_score := ROUND(v_correct_count * v_points_per_question, 1);
        
        -- Update
        UPDATE quiz_attempts 
        SET score = v_new_score
        WHERE id = rec.attempt_id;
        
    END LOOP;
    
    RAISE NOTICE 'Recalculation completed!';
END $$;
