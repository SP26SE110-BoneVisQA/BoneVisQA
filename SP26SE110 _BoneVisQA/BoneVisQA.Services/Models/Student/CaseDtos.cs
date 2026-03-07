using System;
using System.Collections.Generic;

namespace BoneVisQA.Services.Models.Student;

public class CaseListItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Difficulty { get; set; }
    public string? CategoryName { get; set; }
    public string? ThumbnailImageUrl { get; set; }
}

public class MedicalImageDto
{
    public Guid Id { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? Modality { get; set; }
}

public class CaseDetailDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Difficulty { get; set; }
    public string? CategoryName { get; set; }
    public IReadOnlyList<MedicalImageDto> Images { get; set; } = Array.Empty<MedicalImageDto>();
}

