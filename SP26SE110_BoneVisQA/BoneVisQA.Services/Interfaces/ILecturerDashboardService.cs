using BoneVisQA.Services.Models.Lecturer;

namespace BoneVisQA.Services.Interfaces;

public interface ILecturerDashboardService
{
    Task<LecturerDashboardStatsDto> GetDashboardStatsAsync(Guid lecturerId);
    Task<IReadOnlyList<ClassLeaderboardItemDto>> GetClassLeaderboardAsync(Guid lecturerId, Guid classId);
    Task<LecturerAnalyticsDto> GetAnalyticsAsync(Guid lecturerId);

    // Student Progress Methods
    Task<StudentProgressSummaryDto> GetClassStudentProgressAsync(Guid classId);
    Task<StudentProgressDetailDto?> GetStudentProgressDetailAsync(Guid classId, Guid studentId);
    Task<ClassCompetencyOverviewDto> GetClassCompetencyOverviewAsync(Guid classId);
    Task<List<TopicMasteryDto>> GetClassTopicsMasteryAsync(Guid classId);
}
