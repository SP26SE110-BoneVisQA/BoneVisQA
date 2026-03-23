using BoneVisQA.Services.Models.Expert;
using BoneVisQA.Services.Models.Student;
using BoneVisQA.Services.Models.VisualQA;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Interfaces;

public interface IStudentService
{
    Task<IReadOnlyList<CaseListItemDto>> GetCasesAsync(Guid studentId);

    Task<IReadOnlyList<CaseListItemDto>> GetFilteredCasesAsync(Guid studentId, CaseFilterRequestDto filter);

    Task<CaseDetailDto?> GetCaseDetailAsync(Guid caseId, Guid studentId);

    Task<AnnotationDto> CreateAnnotationAsync(Guid studentId, CreateAnnotationRequestDto request);

    Task<StudentQuestionDto> AskQuestionAsync(Guid studentId, AskQuestionRequestDto request);

    Task<StudentQuestionDto> CreateVisualQAQuestionAsync(Guid studentId, VisualQARequestDto request);

    Task SaveVisualQAAnswerAsync(Guid questionId, VisualQAResponseDto response);

    Task<IReadOnlyList<StudentQuestionHistoryItemDto>> GetQuestionHistoryAsync(Guid studentId);

    Task<IReadOnlyList<StudentAnnouncementDto>> GetAnnouncementsAsync(Guid studentId);

    Task<IReadOnlyList<QuizListItemDto>> GetAvailableQuizzesAsync(Guid studentId);

    Task<QuizSessionDto> StartQuizAsync(Guid studentId, Guid quizId);

    Task<QuizResultDto> SubmitQuizAsync(Guid studentId, SubmitQuizRequestDto request);

    Task<StudentProgressDto> GetProgressAsync(Guid studentId);

    Task<StudentSubmitQuestionResponseDTO> StudentSubmitQuestionsAsync(Guid studentid, StudentSubmitQuestionDTO submit);
}

