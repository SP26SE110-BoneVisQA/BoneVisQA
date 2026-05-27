-- =====================================================
-- DEBUG: Xem chi tiết dữ liệu score của một attempt cụ thể
-- =====================================================
-- Thay YOUR_ATTEMPT_ID bằng attempt_id cần kiểm tra
-- =====================================================
WITH attempt_answers AS (
    SELECT 
        qa.id as attempt_id,
        qa.score as stored_score,
        u.full_name as student_name,
        q.title as quiz_title,
        qq.id as question_id,
        qq.type as question_type,
        qq.correct_answer,
        qa2.student_answer,
        qa2.is_correct,
        qa2.score_awarded,
        qa2.is_graded
    FROM quiz_attempts qa
    JOIN quizzes q ON q.id = qa.quiz_id
    JOIN users u ON u.id = qa.student_id
    JOIN student_quiz_answers qa2 ON qa2.attempt_id = qa.id
    JOIN quiz_questions qq ON qq.id = qa2.question_id
    WHERE qa.id = '0fe55a6f-4adc-47c3-8a68-4b08e9be9ac5'  -- Thay bằng attempt_id của bạn
)
SELECT 
    attempt_id,
    student_name,
    quiz_title,
    stored_score,
    question_id,
    CASE 
        WHEN question_type = 0 THEN 'MultipleChoice'
        WHEN question_type = 1 THEN 'TrueFalse'
        WHEN question_type = 2 THEN 'MultiSelect'
        WHEN question_type = 3 THEN 'Essay'
        WHEN question_type = 4 THEN 'FillInBlank'
        ELSE 'Unknown'
    END as question_type,
    correct_answer,
    student_answer,
    is_correct,
    score_awarded,
    is_graded
FROM attempt_answers
ORDER BY question_id;

-- =====================================================
-- Tính lại điểm đúng:
-- =====================================================
WITH question_counts AS (
    SELECT quiz_id, COUNT(*) as total_questions
    FROM quiz_questions
    GROUP BY quiz_id
),
answer_details AS (
    SELECT 
        qa.id as attempt_id,
        u.full_name as student_name,
        qc.total_questions,
        CASE 
            WHEN qc.total_questions > 0 THEN 100.0 / qc.total_questions 
            ELSE 0 
        END as points_per_q,
        qq.type as question_type,
        qa2.is_correct,
        qa2.score_awarded
    FROM quiz_attempts qa
    JOIN quizzes q ON q.id = qa.quiz_id
    JOIN users u ON u.id = qa.student_id
    JOIN question_counts qc ON qc.quiz_id = q.id
    JOIN student_quiz_answers qa2 ON qa2.attempt_id = qa.id
    JOIN quiz_questions qq ON qq.id = qa2.question_id
    WHERE qa.id = '0fe55a6f-4adc-47c3-8a68-4b08e9be9ac5'  -- Thay bằng attempt_id của bạn
),
calculated_scores AS (
    SELECT
        attempt_id,
        student_name,
        total_questions,
        points_per_q,
        SUM(
            CASE
                -- Essay: được điểm nếu score_awarded > 0
                WHEN question_type = 3 AND score_awarded > 0 THEN points_per_q
                -- MC/TF: được điểm nếu is_correct = true
                WHEN question_type IN (0, 1) AND is_correct = true THEN points_per_q
                -- MultiSelect: được điểm nếu is_correct = true
                WHEN question_type = 2 AND is_correct = true THEN points_per_q
                -- FillInBlank: được điểm nếu is_correct = true
                WHEN question_type = 4 AND is_correct = true THEN points_per_q
                -- Tất cả các trường hợp khác = 0
                ELSE 0
            END
        ) as correct_earned_points,
        SUM(
            CASE
                -- Essay: cộng điểm thực tế được chấm
                WHEN question_type = 3 THEN COALESCE(score_awarded, 0)
                -- MC/TF/MultiSelect/FillInBlank: điểm = points_per_q nếu đúng, 0 nếu sai
                WHEN question_type IN (0, 1, 2, 4) THEN 
                    CASE WHEN is_correct = true THEN points_per_q ELSE 0 END
                ELSE 0
            END
        ) as total_score
    FROM answer_details
    GROUP BY attempt_id, student_name, total_questions, points_per_q
)
SELECT 
    attempt_id,
    student_name,
    total_questions,
    ROUND(points_per_q, 2) as points_per_q,
    ROUND(total_score, 1) as calculated_score,
    ROUND(correct_earned_points, 1) as correct_earned_points
FROM calculated_scores;
