using BoneVisQA.Services.Models.Lecturer;

namespace BoneVisQA.Services.Interfaces;

public interface ILecturerDashboardService
{
    Task<LecturerDashboardStatsDto> GetDashboardStatsAsync(Guid lecturerId);
    Task<IReadOnlyList<ClassLeaderboardItemDto>> GetClassLeaderboardAsync(Guid lecturerId, Guid classId);
}
