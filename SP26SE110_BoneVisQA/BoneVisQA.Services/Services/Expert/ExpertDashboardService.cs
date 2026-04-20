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

    private IQueryable<VisualQASession> ExpertVisualQaEscalatedQueue(Guid expertId) =>
        ExpertReviewService.QueryExpertScopedEscalatedQueue(_unitOfWork, expertId);

    public async Task<ExpertDashboardStatsDto> GetDashboardStatsAsync(Guid expertId)
    {
        var thisMonthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var totalCases = await _unitOfWork.Context.MedicalCases
            .AsNoTracking()
            .CountAsync(c => c.IsActive == true);

        var totalReviews = await _unitOfWork.Context.ExpertReviews
            .AsNoTracking()
            .CountAsync(r => r.ExpertId == expertId);

        var pendingReviews =
            await ExpertEscalatedQueue(expertId).CountAsync()
            + await ExpertVisualQaEscalatedQueue(expertId).CountAsync();

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
        var caseAnswers = await ExpertEscalatedQueue(expertId)
            .Include(a => a.Question)
                .ThenInclude(q => q.Student)
            .Include(a => a.Question)
                .ThenInclude(q => q.Case)
                    .ThenInclude(mc => mc!.Category)
            .Include(a => a.ExpertReviews)
            .OrderByDescending(a => a.EscalatedAt ?? a.Question.CreatedAt)
            .Take(12)
            .ToListAsync();

        var visualSessions = await ExpertVisualQaEscalatedQueue(expertId)
            .Include(s => s.Student)
            .Include(s => s.Case!)
                .ThenInclude(mc => mc.Category)
            .Include(s => s.Messages)
            .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
            .Take(12)
            .ToListAsync();

        var caseDtoList = caseAnswers.Select(a =>
        {
            var qtext = a.Question.QuestionText ?? string.Empty;
            var atext = a.AnswerText ?? "";
            return new ExpertDashboardPendingReviewDto
            {
                Id = a.Id,
                StudentName = a.Question.Student?.FullName ?? "Unknown",
                CaseTitle = a.Question.Case?.Title ?? "Unknown Case",
                QuestionSnippet = qtext.Length > 100 ? qtext[..100] + "..." : qtext,
                AiAnswerSnippet = atext.Length > 100 ? atext[..100] + "..." : atext,
                SubmittedAt = a.EscalatedAt ?? a.Question.CreatedAt ?? DateTime.UtcNow,
                Priority = a.AiConfidenceScore.HasValue && a.AiConfidenceScore < 0.5 ? "high" : "normal",
                Category = a.Question.Case?.Category?.Name ?? "General"
            };
        }).Select(d => (dto: d, sort: d.SubmittedAt)).ToList();

        var visualDtoList = visualSessions.Select(s =>
        {
            var firstUser = s.Messages
                .Where(m => string.Equals(m.Role, "User", StringComparison.OrdinalIgnoreCase))
                .OrderBy(m => m.CreatedAt)
                .ThenBy(m => m.Id)
                .FirstOrDefault();
            var latestAssistant = s.Messages
                .Where(m => string.Equals(m.Role, "Assistant", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.Id)
                .FirstOrDefault();
            var qtext = firstUser?.Content ?? "";
            var atext = latestAssistant?.Content ?? "";
            var submitted = s.UpdatedAt ?? s.CreatedAt;
            var priority = latestAssistant?.AiConfidenceScore is { } sc && sc < 0.5 ? "high" : "normal";
            return (
                dto: new ExpertDashboardPendingReviewDto
                {
                    Id = s.Id,
                    StudentName = s.Student?.FullName ?? "Unknown",
                    CaseTitle = s.Case?.Title ?? (s.CaseId.HasValue ? "Unknown Case" : "Personal Visual QA"),
                    QuestionSnippet = qtext.Length > 100 ? qtext[..100] + "..." : qtext,
                    AiAnswerSnippet = atext.Length > 100 ? atext[..100] + "..." : atext,
                    SubmittedAt = submitted,
                    Priority = priority,
                    Category = s.Case?.Category?.Name ?? "General"
                },
                sort: submitted);
        }).ToList();

        return caseDtoList
            .Concat(visualDtoList)
            .OrderByDescending(x => x.sort)
            .Take(5)
            .Select(x => x.dto)
            .ToList();
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
