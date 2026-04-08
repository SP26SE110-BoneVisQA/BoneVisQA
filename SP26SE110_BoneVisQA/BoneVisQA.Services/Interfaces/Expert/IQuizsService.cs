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
        //=====================================================   QUIZ  ==========================================================
        Task<PagedResult<GetQuizDTO>> GetQuizDTO(int pageIndex, int pageSize);
        Task<CreateQuizResponseDTO> CreateQuizAsync(CreateQuizRequestDTO request);
        Task<UpdateQuizResponseDTO> UpdateQuizAsync(UpdateQuizRequestDTO update);
        Task<bool> DeleteQuizAsync(Guid quizId);


        //=====================================================   QUESTION  ==========================================================
        Task<List<GetQuizQuestionDTO>> GetQuizQuestionDTO(Guid quizId);
        Task<CreateQuizQuestionResponseDTO> CreateQuizQuestionAsync(Guid quizId, CreateQuizQuestionRequestDTO request);
        Task<UpdateQuizQuestionResponseDTO> UpdateQuizQuestionAsync(UpdateQuizQuestionRequestDTO update);  
        Task<bool> DeleteQuizQuestionAsync(Guid questionId);


        //================================================================================================================
        Task<PagedResult<ClassQuizSessionDTO>> GetAssignQuizDTO(int pageIndex, int pageSize);
        Task<ClassQuizSessionResponseDTO> AssignQuizToClassAsync(AssignQuizRequestDTO dto);
        Task<QuizScoreResultDto> CalculateScoreAsync(Guid attemptId);


        Task<PagedResult<GetClassDTO>> GetAllClass(int pageIndex, int pageSize);
        Task<PagedResult<GetExpertDTO>> GetAllExpert(int pageIndex, int pageSize);

    }
}
