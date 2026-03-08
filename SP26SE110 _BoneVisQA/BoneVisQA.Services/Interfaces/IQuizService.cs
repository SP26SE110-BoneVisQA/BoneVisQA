using BoneVisQA.Services.Models.Expert;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Interfaces
{
    public interface IQuizService
    {
        // Expert tạo quiz
        Task<QuizDTO> CreateQuizAsync(QuizDTO dto);

        // Expert thêm câu hỏi vào quiz
        Task<QuizQuestionDTO> CreateQuizQuestionAsync(QuizQuestionDTO dto);

        // Lấy quiz theo class
        Task<List<QuizDTO>> GetQuizForClassAsync(Guid classId);

        // Student submit answer
        Task<StudentQuizAnswerDTO> SubmitAnswerAsync(StudentQuizAnswerDTO dto);

        // Chấm điểm quiz
        Task<float> GradeQuizAttemptAsync(Guid attemptId);
    }
}
