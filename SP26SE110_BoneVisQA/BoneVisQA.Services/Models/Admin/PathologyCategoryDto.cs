using System;
using System.Text.Json.Serialization;

namespace BoneVisQA.Services.Models.Admin;

/// <summary>
/// DTO for PathologyCategory entity
/// </summary>
public class PathologyCategoryDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public Guid? BoneSpecialtyId { get; set; }
    public string? BoneSpecialtyName { get; set; }
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// DTO for creating a new PathologyCategory
/// </summary>
public class PathologyCategoryCreateDto
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = null!;

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("boneSpecialtyId")]
    public Guid? BoneSpecialtyId { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("displayOrder")]
    public int DisplayOrder { get; set; } = 0;

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// DTO for updating a PathologyCategory
/// </summary>
public class PathologyCategoryUpdateDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("code")]
    public string Code { get; set; } = null!;

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("boneSpecialtyId")]
    public Guid? BoneSpecialtyId { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("displayOrder")]
    public int DisplayOrder { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }
}

/// <summary>
/// Query parameters for listing PathologyCategories
/// </summary>
public class PathologyCategoryQueryDto
{
    public Guid? BoneSpecialtyId { get; set; }
    public bool? IsActive { get; set; }
    public bool IncludeBoneSpecialty { get; set; } = true;
}
