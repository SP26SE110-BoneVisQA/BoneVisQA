using BoneVisQA.Services.Models.Expert;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Interfaces.Expert
{
        public interface IQuizService
    {
        Task<QuizDTO> CreateQuizAsync(QuizDTO request);

        Task<QuizQuestionDTO> CreateQuestionAsync(Guid quizId, QuizQuestionDTO request);

        Task AssignQuizToClassAsync(Guid classId, Guid quizId);
        Task<QuizScoreResultDTO> CalculateScoreAsync(Guid attemptId);
    }
}
