using System.Text.Json.Serialization;

namespace BoneVisQA.Services.Models.Lecturer;

public class TeachingObjectiveItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Topic { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public string Level { get; set; } = "Basic";
    public int Order { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class TeachingObjectivesDto
{
    public Guid ClassId { get; set; }
    public string? ClassName { get; set; }
    public Guid LecturerId { get; set; }
    public string? LecturerName { get; set; }
    public Guid? ExpertId { get; set; }
    public string? ExpertName { get; set; }
    public List<TeachingObjectiveItem> Objectives { get; set; } = new();
    public int TotalObjectives => Objectives.Count;
    public int ActiveObjectives => Objectives.Count(o => o.IsActive);
    public DateTime? LastUpdated { get; set; }
}

public class TeachingObjectiveSuggestionDto
{
    public Guid Id { get; set; }
    public Guid ClassId { get; set; }
    public string? ClassName { get; set; }
    public Guid ExpertId { get; set; }
    public string? ExpertName { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public string Level { get; set; } = "Basic";
    public string Status { get; set; } = "Pending";
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

public class UpdateTeachingObjectivesRequestDto
{
    public List<TeachingObjectiveItem> Objectives { get; set; } = new();
    public bool ReplaceAll { get; set; } = false;
}

public class ConfirmSuggestionRequestDto
{
    public Guid SuggestionId { get; set; }
    public bool Approve { get; set; }
    public string? RejectionReason { get; set; }
    public int? Order { get; set; }
}

public class SuggestObjectiveRequestDto
{
    public Guid ClassId { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public string Level { get; set; } = "Basic";
}

public class TeachingObjectivesResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public TeachingObjectivesDto? Data { get; set; }
    public List<TeachingObjectiveSuggestionDto>? PendingSuggestions { get; set; }
}

public class ExpertTeachingObjectivesDto
{
    public Guid ClassId { get; set; }
    public string? ClassName { get; set; }
    public Guid LecturerId { get; set; }
    public string? LecturerName { get; set; }
    public string? Semester { get; set; }
    public string? FocusLevel { get; set; }
    public string? TargetStudentLevel { get; set; }
    public List<TeachingObjectiveItem> CurrentObjectives { get; set; } = new();
    public List<TeachingObjectiveSuggestionDto> MyPendingSuggestions { get; set; } = new();
    public DateTime? LastUpdated { get; set; }
}
