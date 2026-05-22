-- Check chi tiết từng câu trả lời
SELECT 
    qa2.id AS answer_id,
    qq.id AS question_id,
    qq.type AS question_type,
    qq.type_name,
    qa2.is_correct,
    qa2.score_awarded,
    qa2.is_graded
FROM student_quiz_answers qa2
JOIN quiz_questions qq ON qq.id = qa2.question_id
WHERE qa2.attempt_id = '5870d77e-2c24-4126-9ff5-0f0e2acdd47c'
ORDER BY qq.type, qq.id;
