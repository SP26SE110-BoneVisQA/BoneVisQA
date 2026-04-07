using BoneVisQA.Services.Models.Lecturer;
using BoneVisQA.Services.Models.Quiz;
using BoneVisQA.Services.Models.Student;

namespace BoneVisQA.Services.Interfaces;

public interface IStudentLearningService
{
    Task<QuizSessionDto> GetPracticeQuizAsync(Guid studentId, string? topic);
    Task<QuizResultDto> SubmitQuizAttemptAsync(Guid studentId, SubmitQuizRequestDto request);

    /// Lưu quiz AI vào DB (tạo Quiz + QuizAttempt), trả về session để student bắt đầu làm.
    Task<StudentGeneratedQuizAttemptDto> SaveAndStartGeneratedQuizAsync(
        Guid studentId,
        AIQuizGenerationResultDto generated,
        string? topic,
        string? difficulty);

    Task<StudentProgressDto> GetProgressSummaryAsync(Guid studentId);
    Task<IReadOnlyList<StudentTopicStatDto>> GetTopicStatsAsync(Guid studentId);
    Task<IReadOnlyList<StudentRecentActivityDto>> GetRecentActivityAsync(Guid studentId);
    Task AutoCloseExpiredAttemptsAsync();

    /// Trả về lịch sử tất cả quiz attempt của student (gồm quiz giao + quiz AI tự tạo).
    Task<IReadOnlyList<StudentQuizAttemptSummaryDto>> GetQuizAttemptHistoryAsync(Guid studentId);

    /// Lấy chi tiết đáp án của một quiz attempt đã nộp (để review sau khi nộp).
    Task<QuizAttemptReviewDto> GetQuizAttemptReviewAsync(Guid studentId, Guid attemptId);

    /// Xóa một quiz attempt của student (chỉ xóa attempt, không xóa quiz gốc).
    Task DeleteQuizAttemptAsync(Guid studentId, Guid attemptId);
}
