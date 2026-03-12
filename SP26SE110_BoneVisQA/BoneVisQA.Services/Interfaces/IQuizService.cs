using BoneVisQA.Services.Models.Lecturer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Interfaces
{
    public interface IQuizService
    {
        Task<QuizDto> CreateQuizAsync(QuizDto request);

        Task<QuizQuestionDto> CreateQuestionAsync(Guid quizId, QuizQuestionDto request);

        Task<List<QuizDto>> GetQuizzesByClassAsync(Guid classId);

        Task<List<QuizDto>> RecommendQuizAsync(string topic);
    }
}