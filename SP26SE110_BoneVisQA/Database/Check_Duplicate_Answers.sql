-- Check duplicate answers for attempt
SELECT 
    question_id, 
    COUNT(*) as duplicate_count,
    array_agg(id) as answer_ids
FROM student_quiz_answers
WHERE attempt_id = '5870d77e-2c24-4126-9ff5-0f0e2acdd47c'
GROUP BY question_id
HAVING COUNT(*) > 1;
