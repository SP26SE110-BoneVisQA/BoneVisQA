using BoneVisQA.Services.Models.Lecturer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Interfaces.Expert
{
    public interface IQuizsService
    {
        Task<QuizDto> CreateQuizAsync(QuizDto request);

        Task<QuizQuestionDto> CreateQuestionAsync(Guid quizId, CreateQuizQuestionDto request);

        Task<ClassQuizDto> AssignQuizToClassAsync(Guid classId, Guid quizId);

        Task<QuizScoreResultDto> CalculateScoreAsync(Guid attemptId);

    }
}
