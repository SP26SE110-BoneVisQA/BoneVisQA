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

public class UpdateTeachingObjectivesRequestDto
{
    public List<TeachingObjectiveItem> Objectives { get; set; } = new();
    public bool ReplaceAll { get; set; } = false;
}
