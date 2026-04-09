using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Student;

/// <summary>Placeholder analytics for FE hydration; extend when dashboard metrics are merged.</summary>
[ApiController]
[Route("api/student/analytics")]
[Tags("Student - Analytics")]
[Authorize(Roles = "Student")]
public class StudentAnalyticsController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(StudentAnalyticsSummaryDto), StatusCodes.Status200OK)]
    public ActionResult<StudentAnalyticsSummaryDto> GetSummary()
    {
        return Ok(new StudentAnalyticsSummaryDto());
    }
}

public class StudentAnalyticsSummaryDto
{
    public int QuestionsAsked { get; set; }
    public int CasesViewed { get; set; }
    public int QuizAttempts { get; set; }
    public double? AverageQuizScore { get; set; }
}
