using System.Security.Claims;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Student;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Student;

[ApiController]
[Route("api/student/profile")]
[Authorize(Roles = "Student")]
public class StudentProfileController : ControllerBase
{
    private readonly IStudentProfileService _studentProfileService;

    public StudentProfileController(IStudentProfileService studentProfileService)
    {
        _studentProfileService = studentProfileService;
    }

    [HttpGet]
    public async Task<ActionResult<StudentProfileDto>> GetProfile()
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        var result = await _studentProfileService.GetProfileAsync(studentId.Value);
        return Ok(result);
    }

    [HttpPut]
    public async Task<ActionResult<StudentProfileDto>> UpdateProfile([FromBody] UpdateStudentProfileRequestDto request)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        if (string.IsNullOrWhiteSpace(request.FullName))
            return BadRequest(new { message = "FullName là bắt buộc." });

        var result = await _studentProfileService.UpdateProfileAsync(studentId.Value, request);
        return Ok(result);
    }

    private Guid? GetUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
