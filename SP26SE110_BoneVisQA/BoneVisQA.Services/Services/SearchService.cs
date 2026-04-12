using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Constants;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Search;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Services.Services;

public class SearchService : ISearchService
{
    private readonly IUnitOfWork _unitOfWork;

    public SearchService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<GlobalSearchResponseDto> SearchAsync(Guid userId, IReadOnlyCollection<string> roles, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query q is required.");

        var normalizedRoles = roles
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var like = $"%{query.Trim()}%";

        var response = new GlobalSearchResponseDto();

        if (normalizedRoles.Contains("Student"))
        {
            await PopulateStudentResultsAsync(response, userId, like);
            return response;
        }

        if (normalizedRoles.Contains("Lecturer"))
        {
            await PopulateLecturerResultsAsync(response, userId, like);
            return response;
        }

        if (normalizedRoles.Contains("Admin") || normalizedRoles.Contains("Expert"))
            await PopulateAdminOrExpertResultsAsync(response, like);

        return response;
    }

    private async Task PopulateStudentResultsAsync(GlobalSearchResponseDto response, Guid userId, string like)
    {
        response.Cases = await _unitOfWork.Context.MedicalCases
            .AsNoTracking()
            .Where(c => c.IsApproved == true && c.IsActive == true)
            .Where(c =>
                (c.Title != null && EF.Functions.ILike(c.Title, like)) ||
                (c.Description != null && EF.Functions.ILike(c.Description, like)) ||
                (c.KeyFindings != null && EF.Functions.ILike(c.KeyFindings, like)))
            .OrderByDescending(c => c.CreatedAt)
            .Take(10)
            .Select(c => new GlobalSearchCaseItemDto
            {
                Id = c.Id,
                Title = c.Title ?? string.Empty
            })
            .ToListAsync();

        var utcNow = DateTime.UtcNow;
        response.Quizzes = await _unitOfWork.Context.ClassQuizSessions
            .AsNoTracking()
            .Where(s => _unitOfWork.Context.ClassEnrollments
                .Any(e => e.StudentId == userId && e.ClassId == s.ClassId))
            .Where(s => s.OpenTime == null || s.OpenTime <= utcNow)
            .Where(s => s.CloseTime == null || s.CloseTime >= utcNow)
            .Select(s => s.Quiz)
            .Where(qz =>
                EF.Functions.ILike(qz.Title, like) ||
                (qz.Topic != null && EF.Functions.ILike(qz.Topic, like)))
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

    private async Task PopulateLecturerResultsAsync(GlobalSearchResponseDto response, Guid userId, string like)
    {
        response.Classes = await _unitOfWork.Context.AcademicClasses
            .AsNoTracking()
            .Where(c => c.LecturerId == userId)
            .Where(c => EF.Functions.ILike(c.ClassName, like))
            .OrderBy(c => c.ClassName)
            .Take(10)
            .Select(c => new GlobalSearchClassItemDto
            {
                Id = c.Id,
                ClassName = c.ClassName
            })
            .ToListAsync();

        response.Users = await _unitOfWork.Context.ClassEnrollments
            .AsNoTracking()
            .Where(e => _unitOfWork.Context.AcademicClasses
                .Any(c => c.LecturerId == userId && c.Id == e.ClassId))
            .Select(e => e.Student)
            .Where(u =>
                EF.Functions.ILike(u.FullName, like) ||
                EF.Functions.ILike(u.Email, like))
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
            .Where(cc => _unitOfWork.Context.AcademicClasses
                .Any(c => c.LecturerId == userId && c.Id == cc.ClassId))
            .Select(cc => cc.Case)
            .Where(c =>
                EF.Functions.ILike(c.Title, like) ||
                (c.Description != null && EF.Functions.ILike(c.Description, like)))
            .Distinct()
            .OrderByDescending(c => c.CreatedAt)
            .Take(10)
            .Select(c => new GlobalSearchCaseItemDto
            {
                Id = c.Id,
                Title = c.Title ?? string.Empty
            })
            .ToListAsync();
    }

    private async Task PopulateAdminOrExpertResultsAsync(GlobalSearchResponseDto response, string like)
    {
        response.Documents = await _unitOfWork.Context.Documents
            .AsNoTracking()
            .Where(d =>
                EF.Functions.ILike(d.Title, like) ||
                (d.FilePath != null && EF.Functions.ILike(d.FilePath, like)))
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
            .Where(u =>
                EF.Functions.ILike(u.FullName, like) ||
                EF.Functions.ILike(u.Email, like))
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
            .Where(a => a.Status == CaseAnswerStatuses.EscalatedToExpert || a.Status == CaseAnswerStatuses.Escalated)
            .Where(a =>
                EF.Functions.ILike(a.Question.QuestionText, like) ||
                (a.AnswerText != null && EF.Functions.ILike(a.AnswerText, like)))
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
}
