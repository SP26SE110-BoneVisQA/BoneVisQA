using System;
using System.Text.Json.Serialization;

namespace BoneVisQA.Services.Models.Admin;

/// <summary>
/// DTO for BoneSpecialty entity
/// </summary>
public class BoneSpecialtyDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public Guid? ParentId { get; set; }
    public string? ParentName { get; set; }
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int Level { get; set; } // 0 = root, 1 = level 1, 2 = level 2
    public List<BoneSpecialtyDto> Children { get; set; } = new();
}

/// <summary>
/// DTO for creating a new BoneSpecialty
/// </summary>
public class BoneSpecialtyCreateDto
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = null!;

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("parentId")]
    public Guid? ParentId { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("displayOrder")]
    public int DisplayOrder { get; set; } = 0;

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// DTO for updating a BoneSpecialty
/// </summary>
public class BoneSpecialtyUpdateDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("code")]
    public string Code { get; set; } = null!;

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("parentId")]
    public Guid? ParentId { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("displayOrder")]
    public int DisplayOrder { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }
}

/// <summary>
/// Query parameters for listing BoneSpecialties
/// </summary>
public class BoneSpecialtyQueryDto
{
    public Guid? ParentId { get; set; }
    public bool? IsActive { get; set; }
    public bool IncludeChildren { get; set; } = false;
    public bool FlatList { get; set; } = true; // false = hierarchical
}
