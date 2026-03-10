using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Models.Admin
{
    public class SystemOverviewDTO
    {
        // User
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int NewUsersThisMonth { get; set; }
        public int PendingUsers { get; set; }        

        // Hoạt động
        public int TotalCaseViews { get; set; }
        public int TotalStudentQuestions { get; set; }
        public int TotalQuizAttempts { get; set; }
        public float AvgQuizScore { get; set; }

        // RAG
        public int TotalDocuments { get; set; }
        public int TotalChunks { get; set; }
        public int TotalCitations { get; set; }

        // Expert Review
        public int TotalReviews { get; set; }
        public int ApprovedReviews { get; set; }
        public int RejectedReviews { get; set; }
    }

    // Thống kê user chi tiết
    public class UserStatDTO
    {
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int InactiveUsers { get; set; }
        public int PendingUsers { get; set; }
        public int NewUsersThisMonth { get; set; }
        public Dictionary<string, int> UsersByRole { get; set; } = new(); 
    }

    // Thống kê hoạt động
    public class ActivityStatDTO
    {
        public int TotalCaseViews { get; set; }
        public int TotalStudentQuestions { get; set; }
        public int TotalQuizAttempts { get; set; }
        public float AvgQuizScore { get; set; }
        public List<DailyActivityDTO> DailyActivity { get; set; } = new(); 
    }

    public class DailyActivityDTO
    {
        public DateTime Date { get; set; }
        public int CaseViews { get; set; }
        public int Questions { get; set; }
        public int QuizAttempts { get; set; }
    }

    // Thống kê RAG
    public class RagStatDTO
    {
        public int TotalDocuments { get; set; }
        public int OutdatedDocuments { get; set; }
        public int TotalChunks { get; set; }
        public int TotalCitations { get; set; }
        public List<TopCitedDocumentDTO> TopCitedDocuments { get; set; } = new();
    }

    public class TopCitedDocumentDTO
    {
        public Guid DocumentId { get; set; }
        public string Title { get; set; } = null!;
        public int CitationCount { get; set; }
    }

    // Thống kê Expert Review
    public class ExpertReviewStatDTO
    {
        public int TotalReviews { get; set; }
        public int ApprovedReviews { get; set; }
        public int RejectedReviews { get; set; }
        public int PendingAnswers { get; set; }     
    }
}
