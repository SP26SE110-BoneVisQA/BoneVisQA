using System.Security.Claims;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private static readonly string[] AllowedAvatarExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private const long MaxAvatarBytes = 5 * 1024 * 1024;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ISupabaseStorageService _storageService;

    public UsersController(IUnitOfWork unitOfWork, ISupabaseStorageService storageService)
    {
        _unitOfWork = unitOfWork;
        _storageService = storageService;
    }

    /// <summary>
    /// Uploads avatar for the authenticated user and updates AvatarUrl.
    /// </summary>
    [HttpPost("me/avatar")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxAvatarBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxAvatarBytes)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<object>> UploadMyAvatar([FromForm] IFormFile? file)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Avatar file is required." });
        if (file.Length > MaxAvatarBytes)
            return BadRequest(new { message = "Avatar file exceeds the 5MB limit." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedAvatarExtensions.Contains(ext))
            return BadRequest(new { message = "Only JPG, PNG, and WebP images are allowed." });

        var user = await _unitOfWork.Context.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);
        if (user == null)
            return NotFound(new { message = "Không tìm thấy người dùng." });

        var avatarUrl = await _storageService.UploadFileAsync(file, "avatars", $"users/{user.Id}");
        user.AvatarUrl = avatarUrl;
        user.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.UserRepository.UpdateAsync(user);
        await _unitOfWork.SaveAsync();

        return Ok(new { avatarUrl });
    }

    private Guid? GetUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
