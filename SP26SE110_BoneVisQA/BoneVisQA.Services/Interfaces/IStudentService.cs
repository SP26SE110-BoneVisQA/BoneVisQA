using BoneVisQA.Services.Models.Lecturer;
using BoneVisQA.Services.Models.Student;
using BoneVisQA.Services.Models.VisualQA;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Interfaces;

public interface IStudentService
{
    Task<IReadOnlyList<CaseListItemDto>> GetCaseCatalogAsync(CaseFilterRequestDto? filter = null);
    Task<CaseCatalogFiltersDto> GetCaseCatalogFiltersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CaseListItemDto>> GetCasesAsync(Guid studentId);

    Task<IReadOnlyList<CaseListItemDto>> GetFilteredCasesAsync(Guid studentId, CaseFilterRequestDto filter);

    Task<CaseDetailDto?> GetCaseDetailAsync(Guid caseId, Guid studentId);

    Task<IReadOnlyList<StudentCaseHistoryItemDto>> GetCaseHistoryAsync(Guid studentId);

    Task<AnnotationDto> CreateAnnotationAsync(Guid studentId, CreateAnnotationRequestDto request);

    Task<StudentQuestionDto> AskQuestionAsync(Guid studentId, AskQuestionRequestDto request);

    Task<Guid> CreateOrGetVisualQaSessionAsync(Guid studentId, VisualQARequestDto request);

    Task SaveVisualQAMessagesAsync(Guid sessionId, VisualQARequestDto request, VisualQAResponseDto response);
    Task ValidateSessionStateAsync(Guid studentId, Guid sessionId, int maxUserQuestions = 3);

    Task RequestVisualQaReviewAsync(Guid studentId, Guid sessionId);
    Task RequestSupportAsync(Guid studentId, Guid answerId, CancellationToken cancellationToken = default);

    /// <summary>Visual QA sessions for the student, newest first.</summary>
    Task<PagedResultDto<VisualQaSessionHistoryItemDto>> GetVisualQaHistoryAsync(
        Guid studentId,
        int limit = 20,
        int offset = 0,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StudentQuestionHistoryItemDto>> GetQuestionHistoryAsync(Guid studentId);

    Task<IReadOnlyList<StudentAnnouncementDto>> GetAnnouncementsAsync(Guid studentId);

    Task<IReadOnlyList<QuizListItemDto>> GetAvailableQuizzesAsync(Guid studentId);

    Task<QuizSessionDto> StartQuizAsync(Guid studentId, Guid quizId);

    // Task<QuizResultDto> SubmitQuizAsync(Guid studentId, SubmitQuizRequestDto request);

    Task<StudentProgressDto> GetProgressAsync(Guid studentId);

    Task<StudentSubmitQuestionResponseDto> SubmitQuizAsync(Guid studentid, StudentSubmitQuestionDto submit);

    /// <summary>
    /// Return the list of classes the student has enrolled in.
    /// </summary>
    Task<IReadOnlyList<StudentClassDto>> GetEnrolledClassesAsync(Guid studentId);

    /// <summary>
    /// Trả về chi tiết đầy đủ của một lớp học (quiz, sinh viên, thông báo) — chỉ khi student đã đăng ký lớp đó.
    /// </summary>
    Task<StudentClassDetailDto> GetClassDetailAsync(Guid studentId, Guid classId);

    /// <summary>
    /// Student tự rời lớp (xóa ClassEnrollment của chính mình).
    /// </summary>
    Task LeaveEnrolledClassAsync(Guid studentId, Guid classId);

    /// <summary>
    /// Student gửi yêu cầu làm lại quiz — tạo notification + email cho lecturer của lớp gán quiz.
    /// </summary>
    Task RequestRetakeAsync(Guid studentId, Guid quizId);
}

