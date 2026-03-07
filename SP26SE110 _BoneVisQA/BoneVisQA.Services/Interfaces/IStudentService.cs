using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BoneVisQA.Services.Models.Student;

namespace BoneVisQA.Services.Interfaces;

public interface IStudentService
{
    Task<IReadOnlyList<CaseListItemDto>> GetCasesAsync(Guid studentId);

    Task<CaseDetailDto?> GetCaseDetailAsync(Guid caseId, Guid studentId);

    Task<AnnotationDto> CreateAnnotationAsync(Guid studentId, CreateAnnotationRequestDto request);

    Task<StudentQuestionDto> AskQuestionAsync(Guid studentId, AskQuestionRequestDto request);

    Task<IReadOnlyList<StudentQuestionHistoryItemDto>> GetQuestionHistoryAsync(Guid studentId);

    Task<IReadOnlyList<QuizListItemDto>> GetAvailableQuizzesAsync(Guid studentId);

    Task<QuizSessionDto> StartQuizAsync(Guid studentId, Guid quizId);

    Task<QuizResultDto> SubmitQuizAsync(Guid studentId, SubmitQuizRequestDto request);

    Task<StudentProgressDto> GetProgressAsync(Guid studentId);
}

