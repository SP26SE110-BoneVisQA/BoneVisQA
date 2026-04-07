using BoneVisQA.Services.Models.Lecturer;
using BoneVisQA.Services.Models.Student;
using BoneVisQA.Services.Models.VisualQA;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Interfaces;

public interface IStudentService
{
    Task<IReadOnlyList<CaseListItemDto>> GetCaseCatalogAsync(CaseFilterRequestDto? filter = null);
    Task<IReadOnlyList<CaseListItemDto>> GetCasesAsync(Guid studentId);

    Task<IReadOnlyList<CaseListItemDto>> GetFilteredCasesAsync(Guid studentId, CaseFilterRequestDto filter);

    Task<CaseDetailDto?> GetCaseDetailAsync(Guid caseId, Guid studentId);

    Task<IReadOnlyList<StudentCaseHistoryItemDto>> GetCaseHistoryAsync(Guid studentId);

    Task<AnnotationDto> CreateAnnotationAsync(Guid studentId, CreateAnnotationRequestDto request);

    Task<StudentQuestionDto> AskQuestionAsync(Guid studentId, AskQuestionRequestDto request);

    Task<StudentQuestionDto> CreateVisualQAQuestionAsync(Guid studentId, VisualQARequestDto request);

    Task SaveVisualQAAnswerAsync(Guid questionId, VisualQAResponseDto response);

    Task<IReadOnlyList<StudentQuestionHistoryItemDto>> GetQuestionHistoryAsync(Guid studentId);

    Task<IReadOnlyList<StudentAnnouncementDto>> GetAnnouncementsAsync(Guid studentId);

    Task<IReadOnlyList<QuizListItemDto>> GetAvailableQuizzesAsync(Guid studentId);

    Task<QuizSessionDto> StartQuizAsync(Guid studentId, Guid quizId);

    // Task<QuizResultDto> SubmitQuizAsync(Guid studentId, SubmitQuizRequestDto request);

    Task<StudentProgressDto> GetProgressAsync(Guid studentId);

    Task<StudentSubmitQuestionResponseDto> SubmitQuizAsync(Guid studentid, StudentSubmitQuestionDto submit);

    /// <summary>
    /// Trả về danh sách lớp học mà sinh viên đã đăng ký.
    /// </summary>
    Task<IReadOnlyList<StudentClassDto>> GetEnrolledClassesAsync(Guid studentId);
}

