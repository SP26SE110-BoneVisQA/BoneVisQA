using System.Text.Json;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Lecturer;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Services.Services.Lecturer;

public class TeachingObjectiveService : ITeachingObjectiveService
{
    private readonly IUnitOfWork _unitOfWork;

    public TeachingObjectiveService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    #region Expert Methods

    public async Task<ExpertTeachingObjectivesDto?> GetClassObjectivesForExpertAsync(Guid expertId, Guid classId)
    {
        var assignment = await _unitOfWork.Context.ClassExpertAssignments
            .Include(a => a.Class)
                .ThenInclude(c => c.Lecturer)
            .FirstOrDefaultAsync(a => a.ClassId == classId && a.ExpertId == expertId && a.IsActive);

        if (assignment == null)
            return null;

        var classEntity = assignment.Class;
        var suggestions = await _unitOfWork.Context.TeachingObjectiveSuggestions
            .Where(s => s.ClassId == classId && s.ExpertId == expertId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        var objectives = ParseObjectives(classEntity.TeachingObjectives);

        return new ExpertTeachingObjectivesDto
        {
            ClassId = classId,
            ClassName = classEntity.ClassName,
            LecturerId = classEntity.LecturerId ?? Guid.Empty,
            LecturerName = classEntity.Lecturer?.FullName,
            Semester = classEntity.Semester,
            FocusLevel = classEntity.FocusLevel,
            TargetStudentLevel = classEntity.TargetStudentLevel,
            CurrentObjectives = objectives,
            MyPendingSuggestions = suggestions
                .Where(s => s.Status == "Pending")
                .Select(s => new TeachingObjectiveSuggestionDto
                {
                    Id = s.Id,
                    ClassId = s.ClassId,
                    ExpertId = s.ExpertId,
                    ExpertName = assignment.Expert.FullName,
                    Topic = s.Topic,
                    Objective = s.Objective,
                    Level = s.Level,
                    Status = s.Status,
                    RejectionReason = s.RejectionReason,
                    CreatedAt = s.CreatedAt,
                    ReviewedAt = s.ReviewedAt
                }).ToList(),
            LastUpdated = classEntity.UpdatedAt
        };
    }

    public async Task<List<ExpertTeachingObjectivesDto>> GetAssignedClassesObjectivesAsync(Guid expertId)
    {
        var assignments = await _unitOfWork.Context.ClassExpertAssignments
            .Include(a => a.Class)
                .ThenInclude(c => c.Lecturer)
            .Where(a => a.ExpertId == expertId && a.IsActive)
            .ToListAsync();

        var result = new List<ExpertTeachingObjectivesDto>();

        foreach (var assignment in assignments)
        {
            var dto = await GetClassObjectivesForExpertAsync(expertId, assignment.ClassId);
            if (dto != null)
                result.Add(dto);
        }

        return result;
    }

    public async Task<TeachingObjectiveSuggestionDto> SuggestObjectiveAsync(Guid expertId, SuggestObjectiveRequestDto request)
    {
        var assignment = await _unitOfWork.Context.ClassExpertAssignments
            .FirstOrDefaultAsync(a => a.ClassId == request.ClassId && a.ExpertId == expertId && a.IsActive);

        if (assignment == null)
            throw new UnauthorizedAccessException("You don't have access to suggest objectives for this class.");

        var expert = await _unitOfWork.Context.Users.FindAsync(expertId);
        if (expert == null)
            throw new KeyNotFoundException("Expert not found.");

        var suggestion = new TeachingObjectiveSuggestion
        {
            Id = Guid.NewGuid(),
            ClassId = request.ClassId,
            ExpertId = expertId,
            Topic = request.Topic,
            Objective = request.Objective,
            Level = request.Level,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        _unitOfWork.Context.TeachingObjectiveSuggestions.Add(suggestion);
        await _unitOfWork.SaveAsync();

        return new TeachingObjectiveSuggestionDto
        {
            Id = suggestion.Id,
            ClassId = suggestion.ClassId,
            ExpertId = suggestion.ExpertId,
            ExpertName = expert.FullName,
            Topic = suggestion.Topic,
            Objective = suggestion.Objective,
            Level = suggestion.Level,
            Status = suggestion.Status,
            CreatedAt = suggestion.CreatedAt
        };
    }

    public async Task<List<TeachingObjectiveSuggestionDto>> GetMyPendingSuggestionsAsync(Guid expertId)
    {
        var suggestions = await _unitOfWork.Context.TeachingObjectiveSuggestions
            .Include(s => s.Expert)
            .Include(s => s.Class)
            .Where(s => s.ExpertId == expertId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return suggestions.Select(s => new TeachingObjectiveSuggestionDto
        {
            Id = s.Id,
            ClassId = s.ClassId,
            ClassName = s.Class?.ClassName,
            ExpertId = s.ExpertId,
            ExpertName = s.Expert?.FullName,
            Topic = s.Topic,
            Objective = s.Objective,
            Level = s.Level,
            Status = s.Status,
            RejectionReason = s.RejectionReason,
            CreatedAt = s.CreatedAt,
            ReviewedAt = s.ReviewedAt
        }).ToList();
    }

    #endregion

    #region Lecturer Methods

    public async Task<TeachingObjectivesDto?> GetTeachingObjectivesAsync(Guid lecturerId, Guid? classId = null)
    {
        var query = _unitOfWork.Context.AcademicClasses
            .Include(c => c.Lecturer)
            .Include(c => c.Expert)
            .Where(c => c.LecturerId == lecturerId);

        if (classId.HasValue)
            query = query.Where(c => c.Id == classId.Value);

        var classEntity = await query.FirstOrDefaultAsync();
        if (classEntity == null)
            return null;

        var objectives = ParseObjectives(classEntity.TeachingObjectives);

        return new TeachingObjectivesDto
        {
            ClassId = classEntity.Id,
            ClassName = classEntity.ClassName,
            LecturerId = lecturerId,
            LecturerName = classEntity.Lecturer?.FullName,
            ExpertId = classEntity.ExpertId,
            ExpertName = classEntity.Expert?.FullName,
            Objectives = objectives,
            LastUpdated = classEntity.UpdatedAt
        };
    }

    public async Task<TeachingObjectivesDto> UpdateTeachingObjectivesAsync(Guid lecturerId, Guid classId, UpdateTeachingObjectivesRequestDto request)
    {
        var classEntity = await _unitOfWork.Context.AcademicClasses
            .Include(c => c.Lecturer)
            .Include(c => c.Expert)
            .FirstOrDefaultAsync(c => c.Id == classId && c.LecturerId == lecturerId);

        if (classEntity == null)
            throw new KeyNotFoundException("Class not found or you don't have permission.");

        var json = JsonSerializer.Serialize(request.Objectives);
        classEntity.TeachingObjectives = json;
        classEntity.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveAsync();

        return new TeachingObjectivesDto
        {
            ClassId = classEntity.Id,
            ClassName = classEntity.ClassName,
            LecturerId = lecturerId,
            LecturerName = classEntity.Lecturer?.FullName,
            ExpertId = classEntity.ExpertId,
            ExpertName = classEntity.Expert?.FullName,
            Objectives = request.Objectives,
            LastUpdated = classEntity.UpdatedAt
        };
    }

    public async Task<List<TeachingObjectiveSuggestionDto>> GetExpertSuggestionsAsync(Guid classId)
    {
        var suggestions = await _unitOfWork.Context.TeachingObjectiveSuggestions
            .Include(s => s.Expert)
            .Include(s => s.Class)
            .Where(s => s.ClassId == classId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return suggestions.Select(s => new TeachingObjectiveSuggestionDto
        {
            Id = s.Id,
            ClassId = s.ClassId,
            ClassName = s.Class?.ClassName,
            ExpertId = s.ExpertId,
            ExpertName = s.Expert?.FullName,
            Topic = s.Topic,
            Objective = s.Objective,
            Level = s.Level,
            Status = s.Status,
            RejectionReason = s.RejectionReason,
            CreatedAt = s.CreatedAt,
            ReviewedAt = s.ReviewedAt
        }).ToList();
    }

    public async Task<TeachingObjectiveSuggestionDto> ConfirmSuggestionAsync(Guid lecturerId, Guid suggestionId, ConfirmSuggestionRequestDto request)
    {
        var suggestion = await _unitOfWork.Context.TeachingObjectiveSuggestions
            .Include(s => s.Class)
            .Include(s => s.Expert)
            .FirstOrDefaultAsync(s => s.Id == suggestionId);

        if (suggestion == null)
            throw new KeyNotFoundException("Suggestion not found.");

        if (suggestion.Class?.LecturerId != lecturerId)
            throw new UnauthorizedAccessException("You don't have permission to review this suggestion.");

        suggestion.Status = request.Approve ? "Approved" : "Rejected";
        suggestion.RejectionReason = request.Approve ? null : request.RejectionReason;
        suggestion.ReviewedBy = lecturerId;
        suggestion.ReviewedAt = DateTime.UtcNow;

        if (request.Approve)
        {
            var currentObjectives = ParseObjectives(suggestion.Class.TeachingObjectives);
            var newObjective = new TeachingObjectiveItem
            {
                Id = Guid.NewGuid(),
                Topic = suggestion.Topic,
                Objective = suggestion.Objective,
                Level = suggestion.Level,
                Order = request.Order ?? currentObjectives.Count,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            currentObjectives.Add(newObjective);

            suggestion.Class.TeachingObjectives = JsonSerializer.Serialize(currentObjectives);
            suggestion.Class.UpdatedAt = DateTime.UtcNow;
        }

        await _unitOfWork.SaveAsync();

        return new TeachingObjectiveSuggestionDto
        {
            Id = suggestion.Id,
            ClassId = suggestion.ClassId,
            ExpertId = suggestion.ExpertId,
            ExpertName = suggestion.Expert?.FullName,
            Topic = suggestion.Topic,
            Objective = suggestion.Objective,
            Level = suggestion.Level,
            Status = suggestion.Status,
            RejectionReason = suggestion.RejectionReason,
            CreatedAt = suggestion.CreatedAt,
            ReviewedAt = suggestion.ReviewedAt
        };
    }

    #endregion

    #region Helpers

    private List<TeachingObjectiveItem> ParseObjectives(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return new List<TeachingObjectiveItem>();

        try
        {
            return JsonSerializer.Deserialize<List<TeachingObjectiveItem>>(json) ?? new List<TeachingObjectiveItem>();
        }
        catch
        {
            return new List<TeachingObjectiveItem>();
        }
    }

    #endregion
}
