using System.Security.Claims;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BoneVisQA.API.Controllers.Admin;

[ApiController]
[Route("api/admin/profile")]
[Authorize(Roles = "Admin")]
public class AdminProfileController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public AdminProfileController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public class AdminProfileDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public bool IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? LastLogin { get; set; }
        public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();

        public DateOnly? DateOfBirth { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Gender { get; set; }
        public string? StudentSchoolId { get; set; }
        public string? ClassCode { get; set; }
        public string? Address { get; set; }
        public string? Bio { get; set; }
        public string? EmergencyContact { get; set; }
    }

    public class UpdateAdminProfileRequestDto
    {
        public string FullName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }

        public DateOnly? DateOfBirth { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Gender { get; set; }
        public string? StudentSchoolId { get; set; }
        public string? ClassCode { get; set; }
        public string? Address { get; set; }
        public string? Bio { get; set; }
        public string? EmergencyContact { get; set; }
    }

    private static AdminProfileDto ToDto(BoneVisQA.Repositories.Models.User user) => new()
    {
        Id = user.Id,
        FullName = user.FullName,
        Email = user.Email,
        AvatarUrl = user.AvatarUrl,
        IsActive = user.IsActive,
        CreatedAt = user.CreatedAt,
        UpdatedAt = user.UpdatedAt,
        LastLogin = user.LastLogin,
        Roles = user.UserRoles.Select(ur => ur.Role.Name).ToList(),
        DateOfBirth = user.DateOfBirth,
        PhoneNumber = user.PhoneNumber,
        Gender = user.Gender,
        StudentSchoolId = user.StudentSchoolId,
        ClassCode = user.ClassCode,
        Address = user.Address,
        Bio = user.Bio,
        EmergencyContact = user.EmergencyContact,
    };

    [HttpGet]
    public async Task<ActionResult<AdminProfileDto>> GetProfile()
    {
        var adminId = GetUserId();
        if (adminId == null)
            return Unauthorized(new { message = "Token khong chua user id hop le." });

        var user = await _unitOfWork.Context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == adminId.Value);

        if (user == null)
            return NotFound(new { message = "Khong tim thay ho so admin." });

        return Ok(ToDto(user));
    }

    [HttpPut]
    public async Task<ActionResult<AdminProfileDto>> UpdateProfile([FromBody] UpdateAdminProfileRequestDto request)
    {
        var adminId = GetUserId();
        if (adminId == null)
            return Unauthorized(new { message = "Token khong chua user id hop le." });

        if (string.IsNullOrWhiteSpace(request.FullName))
            return BadRequest(new { message = "FullName la bat buoc." });

        var user = await _unitOfWork.Context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == adminId.Value);

        if (user == null)
            return NotFound(new { message = "Khong tim thay ho so admin." });

        user.FullName = request.FullName.Trim();
        user.AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl.Trim();
        UserPersonalFieldsHelper.Apply(
            user,
            request.DateOfBirth,
            request.PhoneNumber,
            request.Gender,
            request.StudentSchoolId,
            request.ClassCode,
            request.Address,
            request.Bio,
            request.EmergencyContact);
        user.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.UserRepository.UpdateAsync(user);
        await _unitOfWork.SaveAsync();

        return Ok(ToDto(user));
    }

    private Guid? GetUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
