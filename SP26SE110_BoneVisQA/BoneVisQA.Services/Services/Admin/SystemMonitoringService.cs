using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces.Admin;
using BoneVisQA.Services.Models.Admin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Services.Admin
{
    public class SystemMonitoringService : ISystemMonitoringService
    {
        private readonly IUnitOfWork _unitOfWork;

        public SystemMonitoringService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // ── Thống kê user chi tiết ───────────────────────────
        public async Task<UserStatDTO> GetUserStatsAsync()
        {
            var users = await _unitOfWork.UserRepository.GetAllAsync();
            var userRoles = await _unitOfWork.UserRoleRepository
                .GetAllIncludeAsync(ur => ur.Role);

            var thisMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

            // Lấy userId có role Pending
            var pendingRole = await _unitOfWork.RoleRepository
                .FirstOrDefaultAsync(r => r.Name == "Pending");
            var pendingUserIds = pendingRole != null
                ? (await _unitOfWork.UserRoleRepository
                    .FindAsync(ur => ur.RoleId == pendingRole.Id))
                    .Select(ur => ur.UserId).ToHashSet()
                : new HashSet<Guid>();

            // Đếm user theo từng role
            var usersByRole = userRoles
                .Where(ur => ur.Role != null)
                .GroupBy(ur => ur.Role.Name)
                .ToDictionary(g => g.Key, g => g.Count());

            return new UserStatDTO
            {
                TotalUsers = users.Count,
                ActiveUsers = users.Count(u => u.IsActive),
                InactiveUsers = users.Count(u => !u.IsActive),
                PendingUsers = pendingUserIds.Count,
                NewUsersThisMonth = users.Count(u => u.CreatedAt.HasValue &&
                                        u.CreatedAt.Value >= thisMonth),
                UsersByRole = usersByRole
            };
        }

        // ── Thống kê hoạt động ───────────────────────────────
        public async Task<ActivityStatDTO> GetActivityStatsAsync(DateTimeOffset from, DateTimeOffset to)
        {
            var stats = await _unitOfWork.LearningStatisticRepository.GetAllAsync();

            var caseViews = await _unitOfWork.CaseViewLogRepository
                .FindAsync(c => c.ViewedAt >= from && c.ViewedAt <= to);

            var quizAttempts = await _unitOfWork.QuizAttemptRepository
                .FindAsync(q => q.StartedAt >= from && q.StartedAt <= to);

            var dailyActivity = Enumerable.Range(0, (to - from).Days + 1)
                .Select(i => from.AddDays(i).Date)
                .Select(date => new DailyActivityDTO
                {
                    Date = date,
                    CaseViews = caseViews.Count(c => c.ViewedAt.HasValue &&
                                       c.ViewedAt.Value.Date == date),
                    Questions = 0,
                    QuizAttempts = quizAttempts.Count(q => q.StartedAt.HasValue &&
                                       q.StartedAt.Value.Date == date)
                }).ToList();

            return new ActivityStatDTO
            {
                TotalCaseViews = stats.Sum(s => s.TotalCasesViewed ?? 0),
                TotalStudentQuestions = stats.Sum(s => s.TotalQuestionsAsked ?? 0), 
                TotalQuizAttempts = quizAttempts.Count,
                AvgQuizScore = stats.Any(s => s.AvgQuizScore.HasValue)
                    ? (float)stats.Where(s => s.AvgQuizScore.HasValue)
                                  .Average(s => s.AvgQuizScore!.Value)
                    : 0f,
                DailyActivity = dailyActivity
            };
        }

        // ── Thống kê RAG ─────────────────────────────────────
        public async Task<RagStatDTO> GetRagStatsAsync()
        {
            var documents = await _unitOfWork.DocumentRepository.GetAllAsync();
            var chunks = await _unitOfWork.DocumentChunkRepository.GetAllAsync();
            var citations = await _unitOfWork.CitationRepository.GetAllAsync();

            // Map chunk → doc
            var chunkDocMap = chunks.ToDictionary(c => c.Id, c => c.DocId);

            var topCited = citations
                .Where(c => chunkDocMap.ContainsKey(c.ChunkId))
                .GroupBy(c => chunkDocMap[c.ChunkId])
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g =>
                {
                    var doc = documents.FirstOrDefault(d => d.Id == g.Key);
                    return new TopCitedDocumentDTO
                    {
                        DocumentId = g.Key,
                        Title = doc?.Title ?? "Unknown",
                        CitationCount = g.Count()
                    };
                }).ToList();

            return new RagStatDTO
            {
                TotalDocuments = documents.Count,
                OutdatedDocuments = documents.Count(d => d.IsOutdated),
                TotalChunks = chunks.Count,
                TotalCitations = citations.Count,
                TopCitedDocuments = topCited
            };
        }

        // ── Thống kê Expert Review ───────────────────────────
        public async Task<ExpertReviewStatDTO> GetExpertReviewStatsAsync()
        {
            var reviews = await _unitOfWork.ExpertReviewRepository.GetAllAsync();
            var answers = await _unitOfWork.CaseAnswerRepository.GetAllAsync();

            return new ExpertReviewStatDTO
            {
                TotalReviews = reviews.Count,
                ApprovedReviews = reviews.Count(r => r.Action == "Approve"),
                RejectedReviews = reviews.Count(r => r.Action == "Reject"),
                PendingAnswers = answers.Count(a => a.Status == "Pending")
            };
        }
    }
}
