using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Constants;
using BoneVisQA.Services.Helpers;
using BoneVisQA.Services.Interfaces.Expert;
using BoneVisQA.Services.Models.Expert;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Services.Services.Expert;

public class ExpertDashboardService : IExpertDashboardService
{
    private readonly IUnitOfWork _unitOfWork;

    public ExpertDashboardService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>Escalated by lecturer + student in a class assigned to this expert.</summary>
    private IQueryable<CaseAnswer> ExpertEscalatedQueue(Guid expertId) =>
        _unitOfWork.Context.CaseAnswers
            .AsNoTracking()
            .Where(a =>
                a.Status == CaseAnswerStatuses.EscalatedToExpert ||
                a.Status == CaseAnswerStatuses.Escalated)
            .Where(a =>
                _unitOfWork.Context.ClassEnrollments.Any(e =>
                    e.StudentId == a.Question.StudentId &&
                    e.Class!.ExpertId == expertId));

    public async Task<ExpertDashboardStatsDto> GetDashboardStatsAsync(Guid expertId)
    {
        var thisMonthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var totalCases = await _unitOfWork.Context.MedicalCases
            .AsNoTracking()
            .CountAsync(c => c.IsActive == true);

        var totalReviews = await _unitOfWork.Context.ExpertReviews
            .AsNoTracking()
            .CountAsync(r => r.ExpertId == expertId);

        var pendingReviews = await ExpertEscalatedQueue(expertId).CountAsync();

        var approvedThisMonth = await _unitOfWork.Context.ExpertReviews
            .AsNoTracking()
            .CountAsync(r =>
                r.ExpertId == expertId &&
                (r.Action == "Approved" || r.Action == "Approve") &&
                r.CreatedAt >= thisMonthStart);

        var studentsInExpertClasses = await _unitOfWork.Context.ClassEnrollments
            .AsNoTracking()
            .Where(e => e.Class.ExpertId == expertId)
            .Select(e => e.StudentId)
            .Distinct()
            .CountAsync();

        return new ExpertDashboardStatsDto
        {
            TotalCases = totalCases,
            TotalReviews = totalReviews,
            PendingReviews = pendingReviews,
            ApprovedThisMonth = approvedThisMonth,
            StudentInteractions = studentsInExpertClasses
        };
    }

    public async Task<IReadOnlyList<ExpertDashboardPendingReviewDto>> GetPendingReviewsAsync(Guid expertId)
    {
        var escalated = await ExpertEscalatedQueue(expertId)
            .Include(a => a.Question)
                .ThenInclude(q => q.Student)
            .Include(a => a.Question)
                .ThenInclude(q => q.Case)
                    .ThenInclude(mc => mc!.Category)
            .Include(a => a.ExpertReviews)
            .OrderByDescending(a => a.EscalatedAt ?? a.Question.CreatedAt)
            .Take(5)
            .ToListAsync();

        return escalated.Select(a => new ExpertDashboardPendingReviewDto
        {
            Id = a.Id,
            StudentName = a.Question.Student?.FullName ?? "Unknown",
            CaseTitle = a.Question.Case?.Title ?? "Unknown Case",
            QuestionSnippet = a.Question.QuestionText.Length > 100
                ? a.Question.QuestionText[..100] + "..."
                : a.Question.QuestionText,
            AiAnswerSnippet = a.AnswerText?.Length > 100 == true
                ? a.AnswerText[..100] + "..."
                : a.AnswerText ?? "",
            SubmittedAt = a.EscalatedAt ?? a.Question.CreatedAt ?? DateTime.UtcNow,
            Priority = a.AiConfidenceScore.HasValue && a.AiConfidenceScore < 0.5 ? "high" : "normal",
            Category = a.Question.Case?.Category?.Name ?? "General"
        }).ToList();
    }

    public async Task<IReadOnlyList<ExpertDashboardRecentCaseDto>> GetRecentCasesAsync(Guid expertId)
    {
        return await _unitOfWork.Context.MedicalCases
            .AsNoTracking()
            .Where(c =>
                c.ClassCases.Any(cc => cc.Class.ExpertId == expertId) ||
                c.CreatedByExpertId == expertId)
            .OrderByDescending(c => c.CreatedAt)
            .Take(4)
            .Select(c => new ExpertDashboardRecentCaseDto
            {
                Id = c.Id,
                Title = c.Title ?? "Untitled Case",
                BoneLocation = c.CaseTags
                    .Where(ct => ct.Tag != null &&
                        (ct.Tag.Type == "Location" || ct.Tag.Type == "BoneLocation"))
                    .Select(ct => ct.Tag!.Name)
                    .FirstOrDefault() ?? ExpertMedicalCaseDisplayHelper.DefaultBoneLocation,
                LesionType = c.Category != null ? c.Category.Name : ExpertMedicalCaseDisplayHelper.DefaultCategory,
                Difficulty = c.Difficulty ?? ExpertMedicalCaseDisplayHelper.DefaultDifficulty,
                Status = c.IsApproved == true
                    ? "approved"
                    : (c.IsActive == true ? "pending" : "draft"),
                AddedBy = c.CreatedByExpert != null ? c.CreatedByExpert.FullName : "Unknown",
                AddedDate = c.CreatedAt ?? DateTime.UtcNow,
                ViewCount = c.CaseViewLogs.Count(),
                UsageCount = 0
            })
            .ToListAsync();
    }

    public async Task<ExpertDashboardActivityDto> GetActivityAsync(Guid expertId)
    {
        var now = DateTime.UtcNow;
        var todayUtc = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var startOfWeek = todayUtc.AddDays(-(int)todayUtc.DayOfWeek);

        var reviews = await _unitOfWork.Context.ExpertReviews
            .AsNoTracking()
            .Where(r => r.ExpertId == expertId && r.CreatedAt >= startOfWeek)
            .ToListAsync();

        var days = new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
        var weeklyActivity = Enumerable.Range(0, 7)
            .Select(i =>
            {
                var date = startOfWeek.AddDays(i);
                var dayReviews = reviews.Count(r => r.CreatedAt?.Date == date.Date);
                return new DailyActivityItemDto
                {
                    Day = days[(int)date.DayOfWeek],
                    Reviews = dayReviews,
                    Cases = 0
                };
            }).ToList();

        var totalReviews = weeklyActivity.Sum(d => d.Reviews);
        var avgDaily = totalReviews / 7f;

        return new ExpertDashboardActivityDto
        {
            WeeklyActivity = weeklyActivity,
            AvgDailyReviews = avgDaily
        };
    }
}
