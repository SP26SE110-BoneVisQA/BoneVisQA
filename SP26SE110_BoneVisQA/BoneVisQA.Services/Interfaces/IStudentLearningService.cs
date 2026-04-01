using BoneVisQA.Services.Models.Student;

namespace BoneVisQA.Services.Interfaces;

public interface IStudentLearningService
{
    Task<QuizSessionDto> GetPracticeQuizAsync(Guid studentId, string? topic);
    Task<QuizResultDto> SubmitQuizAttemptAsync(Guid studentId, SubmitQuizRequestDto request);
    Task<StudentProgressDto> GetProgressSummaryAsync(Guid studentId);
    Task<IReadOnlyList<StudentTopicStatDto>> GetTopicStatsAsync(Guid studentId);
    Task<IReadOnlyList<StudentRecentActivityDto>> GetRecentActivityAsync(Guid studentId);
}
