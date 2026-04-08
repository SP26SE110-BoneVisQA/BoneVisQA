using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Student;

/// <summary>Placeholder routes for FE compatibility until assignment list API is fully merged.</summary>
[ApiController]
[Route("api/student/assignments")]
[Tags("Student - Assignments")]
[Authorize(Roles = "Student")]
public class StudentAssignmentsController : ControllerBase
{
    /// <summary>Returns an empty list until class/case assignment listing is wired.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<StudentAssignmentSummaryDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<StudentAssignmentSummaryDto>> GetAssignments()
    {
        return Ok(Array.Empty<StudentAssignmentSummaryDto>());
    }
}

public class StudentAssignmentSummaryDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime? DueAt { get; set; }
}
