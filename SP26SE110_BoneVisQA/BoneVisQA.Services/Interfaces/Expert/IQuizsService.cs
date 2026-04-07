using BoneVisQA.Services.Models.Expert;
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
        Task<QuizResponseDTO> CreateQuizAsync(QuizRequestDTO request);

        Task<QuizQuestionDTO> CreateQuestionAsync(Guid quizId, CreateQuizQuestionDTO request);

        Task<ClassQuizSessionResponseDTO> AssignQuizToClassAsync(AssignQuizRequestDTO dto);

        Task<QuizScoreResultDto> CalculateScoreAsync(Guid attemptId);

    }
}
