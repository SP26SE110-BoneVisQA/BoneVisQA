using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Admin;

[ApiController]
[Route("api/admin/reports")]
[Authorize(Roles = "Admin")]
public class ReportsController : ControllerBase
{
    [HttpGet]
    public ActionResult GetReports([FromQuery] string period = "30d")
    {
        var data = GetReportData(period);
        return Ok(new { success = true, data });
    }

    [HttpGet("stats")]
    public ActionResult GetStats([FromQuery] string period = "30d")
    {
        var data = GetReportData(period);
        return Ok(new { success = true, data = data.Stats });
    }

    [HttpGet("top-cases")]
    public ActionResult GetTopCases([FromQuery] string period = "30d", [FromQuery] int limit = 10)
    {
        return Ok(new
        {
            success = true,
            data = new[]
            {
                new { title = "Distal Radius Fracture", views = 1234, completions = 892, score = 85 },
                new { title = "ACL Tear - MRI Analysis", views = 987, completions = 654, score = 78 },
                new { title = "Osteoarthritis of the Knee", views = 876, completions = 543, score = 72 },
                new { title = "Lumbar Disc Herniation", views = 765, completions = 432, score = 68 },
                new { title = "Shoulder Impingement Syndrome", views = 654, completions = 321, score = 75 },
                new { title = "Hip Fracture Classification", views = 543, completions = 287, score = 82 },
                new { title = "Ankle Sprain Grades", views = 498, completions = 234, score = 88 },
                new { title = "Colles Fracture", views = 432, completions = 198, score = 71 },
                new { title = "Meniscus Tear MRI", views = 387, completions = 176, score = 65 },
                new { title = "Rotator Cuff Injury", views = 356, completions = 154, score = 69 },
            }.Take(limit).ToList()
        });
    }

    [HttpGet("top-quizzes")]
    public ActionResult GetTopQuizzes([FromQuery] string period = "30d", [FromQuery] int limit = 10)
    {
        return Ok(new
        {
            success = true,
            data = new[]
            {
                new { topic = "Upper Extremity Fractures", attempts = 2345, avgScore = 82, passRate = 78 },
                new { topic = "Lower Extremity Anatomy", attempts = 1876, avgScore = 78, passRate = 72 },
                new { topic = "Spine Pathologies", attempts = 1543, avgScore = 71, passRate = 65 },
                new { topic = "Bone Tumors", attempts = 1234, avgScore = 68, passRate = 58 },
                new { topic = "Joint Diseases", attempts = 987, avgScore = 75, passRate = 68 },
                new { topic = "Pediatric Orthopedics", attempts = 876, avgScore = 74, passRate = 67 },
                new { topic = "Sports Injuries", attempts = 765, avgScore = 79, passRate = 73 },
                new { topic = "Fracture Healing", attempts = 654, avgScore = 83, passRate = 76 },
                new { topic = "Imaging Techniques", attempts = 543, avgScore = 77, passRate = 70 },
                new { topic = "Rehabilitation", attempts = 432, avgScore = 81, passRate = 75 },
            }.Take(limit).ToList()
        });
    }

    [HttpGet("user-activity")]
    public ActionResult GetUserActivity([FromQuery] string period = "30d")
    {
        var data = new
        {
            dailyActiveUsers = new[]
            {
                new { date = DateTime.UtcNow.AddDays(-6).ToString("yyyy-MM-dd"), count = 120 },
                new { date = DateTime.UtcNow.AddDays(-5).ToString("yyyy-MM-dd"), count = 145 },
                new { date = DateTime.UtcNow.AddDays(-4).ToString("yyyy-MM-dd"), count = 132 },
                new { date = DateTime.UtcNow.AddDays(-3).ToString("yyyy-MM-dd"), count = 158 },
                new { date = DateTime.UtcNow.AddDays(-2).ToString("yyyy-MM-dd"), count = 167 },
                new { date = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd"), count = 189 },
                new { date = DateTime.UtcNow.ToString("yyyy-MM-dd"), count = 203 },
            },
            newVsReturning = new
            {
                newUsers = 312,
                returningUsers = 2547
            },
            byRole = new
            {
                students = 2800,
                lecturers = 45,
                experts = 12,
                admins = 3
            }
        };

        return Ok(new { success = true, data });
    }

    [HttpGet("export")]
    public ActionResult ExportReport([FromQuery] string period = "30d", [FromQuery] string format = "csv")
    {
        var data = GetReportData(period);
        
        if (format == "json")
        {
            return Ok(new { success = true, data });
        }

        // Generate CSV
        var csv = GenerateCsvReport(data);
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"bonevisqa-report-{period}.csv");
    }

    private ReportData GetReportData(string period)
    {
        return period switch
        {
            "7d" => new ReportData
            {
                Period = "Last 7 days",
                ActiveUsers = 1247,
                NewRegistrations = 89,
                CasesViewed = 4521,
                QuizzesTaken = 892,
                AIQuestions = 2341,
                AvgQuizScore = 78,
                Stats = new ReportStats
                {
                    TotalUsers = 1247,
                    ActiveCases = 156,
                    TotalQuizzes = 89,
                    TotalQASessions = 2341,
                    AvgSessionDuration = 4.5,
                    CompletionRate = 72.5
                }
            },
            "90d" => new ReportData
            {
                Period = "Last 90 days",
                ActiveUsers = 3156,
                NewRegistrations = 567,
                CasesViewed = 45231,
                QuizzesTaken = 8234,
                AIQuestions = 21341,
                AvgQuizScore = 72,
                Stats = new ReportStats
                {
                    TotalUsers = 3156,
                    ActiveCases = 234,
                    TotalQuizzes = 156,
                    TotalQASessions = 21341,
                    AvgSessionDuration = 5.2,
                    CompletionRate = 68.3
                }
            },
            "1y" => new ReportData
            {
                Period = "Last year",
                ActiveUsers = 3156,
                NewRegistrations = 1245,
                CasesViewed = 128456,
                QuizzesTaken = 23456,
                AIQuestions = 67834,
                AvgQuizScore = 70,
                Stats = new ReportStats
                {
                    TotalUsers = 3156,
                    ActiveCases = 312,
                    TotalQuizzes = 245,
                    TotalQASessions = 67834,
                    AvgSessionDuration = 5.8,
                    CompletionRate = 65.2
                }
            },
            _ => new ReportData // 30d default
            {
                Period = "Last 30 days",
                ActiveUsers = 2847,
                NewRegistrations = 312,
                CasesViewed = 18234,
                QuizzesTaken = 3456,
                AIQuestions = 8934,
                AvgQuizScore = 75,
                Stats = new ReportStats
                {
                    TotalUsers = 2847,
                    ActiveCases = 189,
                    TotalQuizzes = 123,
                    TotalQASessions = 8934,
                    AvgSessionDuration = 4.8,
                    CompletionRate = 70.1
                }
            }
        };
    }

    private string GenerateCsvReport(ReportData data)
    {
        var lines = new List<string>
        {
            "Metric,Value",
            $"Period,{data.Period}",
            $"Active Users,{data.ActiveUsers}",
            $"New Registrations,{data.NewRegistrations}",
            $"Cases Viewed,{data.CasesViewed}",
            $"Quizzes Taken,{data.QuizzesTaken}",
            $"AI Questions,{data.AIQuestions}",
            $"Average Quiz Score,{data.AvgQuizScore}%"
        };
        return string.Join("\n", lines);
    }
}

public class ReportData
{
    public string Period { get; set; } = string.Empty;
    public int ActiveUsers { get; set; }
    public int NewRegistrations { get; set; }
    public int CasesViewed { get; set; }
    public int QuizzesTaken { get; set; }
    public int AIQuestions { get; set; }
    public int AvgQuizScore { get; set; }
    public ReportStats Stats { get; set; } = new();
}

public class ReportStats
{
    public int TotalUsers { get; set; }
    public int ActiveCases { get; set; }
    public int TotalQuizzes { get; set; }
    public int TotalQASessions { get; set; }
    public double AvgSessionDuration { get; set; }
    public double CompletionRate { get; set; }
}
