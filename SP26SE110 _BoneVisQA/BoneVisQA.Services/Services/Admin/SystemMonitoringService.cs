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

        // ── Dashboard tổng quan ──────────────────────────────
        public async Task<SystemOverviewDTO> GetOverviewAsync()
        {
            var users = await _unitOfWork.UserRepository.GetAllAsync();
            var caseViews = await _unitOfWork.CaseViewLogRepository.GetAllAsync();
            var studentQuestions = await _unitOfWork.StudentQuestionRepository.GetAllAsync();
            var quizAttempts = await _unitOfWork.QuizAttemptRepository.GetAllAsync();
            var documents = await _unitOfWork.DocumentRepository.GetAllAsync();
            var chunks = await _unitOfWork.DocumentChunkRepository.GetAllAsync();
            var citations = await _unitOfWork.CitationRepository.GetAllAsync();
            var reviews = await _unitOfWork.ExpertReviewRepository.GetAllAsync();

            var thisMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

            // Lấy userId có role Pending
            var pendingRole = await _unitOfWork.RoleRepository
                .FirstOrDefaultAsync(r => r.Name == "Pending");
            var pendingUserIds = pendingRole != null
                ? (await _unitOfWork.UserRoleRepository
                    .FindAsync(ur => ur.RoleId == pendingRole.Id))
                    .Select(ur => ur.UserId).ToHashSet()
                : new HashSet<Guid>();

            return new SystemOverviewDTO
            {
                // User
                TotalUsers = users.Count,
                ActiveUsers = users.Count(u => u.IsActive),
                NewUsersThisMonth = users.Count(u => u.CreatedAt >= thisMonth),
                PendingUsers = pendingUserIds.Count,

                // Hoạt động
                TotalCaseViews = caseViews.Count,
                TotalStudentQuestions = studentQuestions.Count,
                TotalQuizAttempts = quizAttempts.Count,
                AvgQuizScore = quizAttempts.Any()
                    ? (float)quizAttempts.Average(q => q.Score)
                    : 0f,

                // RAG
                TotalDocuments = documents.Count,
                TotalChunks = chunks.Count,
                TotalCitations = citations.Count,

                // Expert Review
                TotalReviews = reviews.Count,
                ApprovedReviews = reviews.Count(r => r.Action == "approved"),
                RejectedReviews = reviews.Count(r => r.Action == "rejected")
            };
        }

        // ── Thống kê user chi tiết ───────────────────────────
        public async Task<UserStatDTO> GetUserStatsAsync()
        {
            var users = await _unitOfWork.UserRepository.GetAllAsync();
            var userRoles = await _unitOfWork.UserRoleRepository
                .GetAllIncludeAsync(ur => ur.Role);

            var thisMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

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
                NewUsersThisMonth = users.Count(u => u.CreatedAt >= thisMonth),
                UsersByRole = usersByRole
            };
        }

        // ── Thống kê hoạt động theo khoảng thời gian ─────────
        public async Task<ActivityStatDTO> GetActivityStatsAsync(DateTime from, DateTime to)
        {
            var caseViews = await _unitOfWork.CaseViewLogRepository
                .FindAsync(c => c.ViewedAt >= from && c.ViewedAt <= to);
            var questions = await _unitOfWork.StudentQuestionRepository
                .FindAsync(q => q.CreatedAt >= from && q.CreatedAt <= to);
            var quizAttempts = await _unitOfWork.QuizAttemptRepository
                .FindAsync(q => q.StartedAt >= from && q.StartedAt <= to);

            // Group theo ngày
            var dailyActivity = Enumerable.Range(0, (to - from).Days + 1)
                .Select(i => from.AddDays(i).Date)
                .Select(date => new DailyActivityDTO
                {
                    Date = date,
                    CaseViews = caseViews.Count(c => c.ViewedAt.HasValue && c.ViewedAt.Value.Date == date),
                    Questions = questions.Count(q => q.CreatedAt.HasValue &&
                                       q.CreatedAt.Value.Date == date),
                    QuizAttempts = quizAttempts.Count(q => q.StartedAt.HasValue &&
                                       q.StartedAt.Value.Date == date)
                }).ToList();

            return new ActivityStatDTO
            {
                TotalCaseViews = caseViews.Count,
                TotalStudentQuestions = questions.Count,
                TotalQuizAttempts = quizAttempts.Count,
                AvgQuizScore = quizAttempts.Any()
                    ? (float)quizAttempts.Average(q => q.Score)
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

            // Top tài liệu được cite nhiều nhất
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
                ApprovedReviews = reviews.Count(r => r.Action == "approved"),
                RejectedReviews = reviews.Count(r => r.Action == "rejected"),
                PendingAnswers = answers.Count(a => a.Status == "pending")
            };
        }
    }
}
