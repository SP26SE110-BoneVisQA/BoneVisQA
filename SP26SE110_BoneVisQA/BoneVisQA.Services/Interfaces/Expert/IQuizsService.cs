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
        Task<PagedResult<GetQuizDTO>> GetQuizAsync(int pageIndex, int pageSize);
        Task<CreateQuizResponseDTO> CreateQuizAsync(CreateQuizRequestDTO request);
        Task<UpdateQuizResponseDTO> UpdateQuizAsync(UpdateQuizRequestDTO update);
        Task<bool> DeleteQuizAsync(Guid quizId);

        Task RemoveQuizFromClassAsync(Guid classId, Guid quizId);


        //=====================================================   QUESTION  ==========================================================
        Task<List<GetQuizQuestionDTO>> GetQuizQuestionDTO(Guid quizId);
        Task<CreateQuizQuestionResponseDTO> CreateQuizQuestionAsync(Guid quizId, CreateQuizQuestionRequestDTO request);
        Task<UpdateQuizQuestionResponseDTO> UpdateQuizQuestionAsync(UpdateQuizQuestionRequestDTO update);  
        Task<bool> DeleteQuizQuestionAsync(Guid questionId);


        //================================================================================================================
        Task<PagedResult<ClassQuizSessionDTO>> GetAssignQuizDTO(int pageIndex, int pageSize);
        Task<ClassQuizSessionResponseDTO> AssignQuizToClassAsync(AssignQuizRequestDTO dto);
        Task<List<GetQuizAttemptDTO>> GetAttemptsByQuizAsync(Guid quizId);
        Task<QuizScoreResultDto> CalculateScoreAsync(Guid attemptId);


        Task<PagedResult<GetClassDTO>> GetAllClass(int pageIndex, int pageSize);
        Task<PagedResult<GetExpertDTO>> GetAllExpert(int pageIndex, int pageSize);

        //=====================================================   EXPERT QUIZZES FOR LECTURER  ==========================================================
        Task<PagedResult<ExpertQuizForLecturerDto>> GetExpertQuizzesForLecturerAsync(
            int pageIndex,
            int pageSize,
            string? topic = null,
            string? difficulty = null,
            string? classification = null);

        Task<List<ExpertQuizQuestionDto>> GetExpertQuizQuestionsAsync(Guid quizId);
        Task<(bool IsAssigned, int AssignedClassCount)> IsQuizAssignedAsync(Guid quizId);
        Task<CopiedExpertQuizDto> CopyExpertQuizForLecturerAsync(Guid expertQuizId, Guid lecturerId, string? newTitle = null);

        //=====================================================   DEEP CLASSIFICATION  ==========================================================
        Task<List<BoneSpecialtyTreeDto>> GetBoneSpecialtiesTreeAsync();
        Task<List<PathologyCategorySimpleDto>> GetPathologyCategoriesAsync();
    }
}
