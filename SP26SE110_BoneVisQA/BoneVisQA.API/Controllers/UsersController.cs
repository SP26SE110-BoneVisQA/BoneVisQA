using System.Security.Claims;
using BoneVisQA.Repositories.DBContext;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.API.Controllers;

[ApiController]
[Route("api/users")]
[Tags("Users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly BoneVisQADbContext _dbContext;

    public UsersController(BoneVisQADbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? User.FindFirstValue(ClaimTypes.Name)
                        ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(rawUserId, out var userId) || userId == Guid.Empty)
        {
            return Unauthorized(new { message = "Invalid user identity in token." });
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            return NotFound(new { message = "User not found." });
        }

        return Ok(new
        {
            id = user.Id,
            fullName = user.FullName,
            email = user.Email,
            schoolCohort = user.SchoolCohort,
            avatarUrl = user.AvatarUrl,
            medicalSchool = user.MedicalSchool,
            medicalStudentId = user.MedicalStudentId,
            verificationStatus = user.VerificationStatus,
            roles = user.UserRoles
                .Where(ur => ur.Role != null)
                .Select(ur => ur.Role.Name)
                .Distinct()
                .ToList(),
            isActive = user.IsActive,
            createdAt = user.CreatedAt,
            updatedAt = user.UpdatedAt
        });
    }
}
