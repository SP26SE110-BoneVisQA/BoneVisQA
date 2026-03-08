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
        Task<QuizDTO> CreateQuizAsync(QuizDTO request);

        Task<QuizQuestionDTO> CreateQuestionAsync(Guid quizId, QuizQuestionDTO request);

        Task<List<QuizDTO>> GetQuizzesByClassAsync(Guid classId);

        Task<List<QuizDTO>> RecommendQuizAsync(string topic);
    }
}
