using System.Security.Claims;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Constants;
using BoneVisQA.Services.Models.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.API.Controllers;

[ApiController]
[Route("api/search")]
[Tags("Search")]
[Authorize]
public class SearchController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public SearchController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Global role-aware search endpoint.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(GlobalSearchResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<GlobalSearchResponseDto>> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { message = "Query q is required." });

        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        var query = q.Trim();
        var lowered = query.ToLower();
        var response = new GlobalSearchResponseDto();
        var roles = User.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (roles.Contains("Student"))
        {
            response.Cases = await _unitOfWork.Context.MedicalCases
                .AsNoTracking()
                .Where(c => c.IsApproved == true && c.IsActive == true &&
                            ((c.Title != null && c.Title.ToLower().Contains(lowered)) ||
                             (c.Description != null && c.Description.ToLower().Contains(lowered)) ||
                             (c.KeyFindings != null && c.KeyFindings.ToLower().Contains(lowered))))
                .OrderByDescending(c => c.CreatedAt)
                .Take(10)
                .Select(c => new GlobalSearchCaseItemDto { Id = c.Id, Title = c.Title })
                .ToListAsync();

            var classIds = await _unitOfWork.Context.ClassEnrollments
                .AsNoTracking()
                .Where(e => e.StudentId == userId.Value)
                .Select(e => e.ClassId)
                .ToListAsync();

            var utcNow = DateTime.UtcNow;
            response.Quizzes = await _unitOfWork.Context.ClassQuizSessions
                .AsNoTracking()
                .Where(s => classIds.Contains(s.ClassId))
                .Where(s => s.OpenTime == null || s.OpenTime <= utcNow)
                .Where(s => s.CloseTime == null || s.CloseTime >= utcNow)
                .Select(s => s.Quiz)
                .Where(qz => qz.Title.ToLower().Contains(lowered) || (qz.Topic != null && qz.Topic.ToLower().Contains(lowered)))
                .Distinct()
                .OrderByDescending(qz => qz.CreatedAt)
                .Take(10)
                .Select(qz => new GlobalSearchQuizItemDto
                {
                    Id = qz.Id,
                    Title = qz.Title,
                    Topic = qz.Topic
                })
                .ToListAsync();
        }
        else if (roles.Contains("Lecturer"))
        {
            response.Classes = await _unitOfWork.Context.AcademicClasses
                .AsNoTracking()
                .Where(c => c.LecturerId == userId.Value && c.ClassName.ToLower().Contains(lowered))
                .OrderBy(c => c.ClassName)
                .Take(10)
                .Select(c => new GlobalSearchClassItemDto { Id = c.Id, ClassName = c.ClassName })
                .ToListAsync();

            var lecturerClassIds = await _unitOfWork.Context.AcademicClasses
                .AsNoTracking()
                .Where(c => c.LecturerId == userId.Value)
                .Select(c => c.Id)
                .ToListAsync();

            response.Users = await _unitOfWork.Context.ClassEnrollments
                .AsNoTracking()
                .Where(e => lecturerClassIds.Contains(e.ClassId))
                .Select(e => e.Student)
                .Where(u => u.FullName.ToLower().Contains(lowered) || u.Email.ToLower().Contains(lowered))
                .Distinct()
                .OrderBy(u => u.FullName)
                .Take(10)
                .Select(u => new GlobalSearchUserItemDto
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = u.Email
                })
                .ToListAsync();

            response.Cases = await _unitOfWork.Context.ClassCases
                .AsNoTracking()
                .Where(cc => lecturerClassIds.Contains(cc.ClassId))
                .Select(cc => cc.Case)
                .Where(c => c.Title.ToLower().Contains(lowered) || c.Description.ToLower().Contains(lowered))
                .Distinct()
                .OrderByDescending(c => c.CreatedAt)
                .Take(10)
                .Select(c => new GlobalSearchCaseItemDto
                {
                    Id = c.Id,
                    Title = c.Title
                })
                .ToListAsync();
        }
        else if (roles.Contains("Admin") || roles.Contains("Expert"))
        {
            response.Documents = await _unitOfWork.Context.Documents
                .AsNoTracking()
                .Where(d => d.Title.ToLower().Contains(lowered) ||
                            (d.FilePath != null && d.FilePath.ToLower().Contains(lowered)))
                .OrderByDescending(d => d.CreatedAt)
                .Take(10)
                .Select(d => new GlobalSearchDocumentItemDto
                {
                    Id = d.Id,
                    Title = d.Title,
                    IndexingStatus = d.IndexingStatus
                })
                .ToListAsync();

            response.Users = await _unitOfWork.Context.Users
                .AsNoTracking()
                .Where(u => u.FullName.ToLower().Contains(lowered) || u.Email.ToLower().Contains(lowered))
                .OrderBy(u => u.FullName)
                .Take(10)
                .Select(u => new GlobalSearchUserItemDto
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = u.Email
                })
                .ToListAsync();

            response.EscalatedQuestions = await _unitOfWork.Context.CaseAnswers
                .AsNoTracking()
                .Include(a => a.Question)
                .Where(a => a.Status == CaseAnswerStatuses.EscalatedToExpert || a.Status == CaseAnswerStatuses.Escalated)
                .Where(a => (a.Question.QuestionText != null && a.Question.QuestionText.ToLower().Contains(lowered)) ||
                            (a.AnswerText != null && a.AnswerText.ToLower().Contains(lowered)))
                .OrderByDescending(a => a.EscalatedAt)
                .Take(10)
                .Select(a => new GlobalSearchEscalatedQuestionItemDto
                {
                    AnswerId = a.Id,
                    QuestionId = a.QuestionId,
                    QuestionText = a.Question.QuestionText,
                    CurrentAnswerText = a.AnswerText,
                    EscalatedAt = a.EscalatedAt
                })
                .ToListAsync();
        }

        return Ok(response);
    }

    private Guid? GetUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
