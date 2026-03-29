using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace BoneVisQA.Services.Models.VisualQA;

public class VisualQARequestDto
{
    [DefaultValue("Vùng khoanh đỏ trên ảnh có dấu hiệu gãy xương không?")]
    public string QuestionText { get; set; } = string.Empty;

    [DefaultValue(null)]
    public string? ImageUrl { get; set; }

    [DefaultValue(null)]
    public string? Coordinates { get; set; }

    /// <summary>
    /// Optional Case ID. Leave null for NEW personal uploads.
    /// </summary>
    public Guid? CaseId { get; set; }

    /// <summary>
    /// Optional Annotation ID. Provide for inquiries on existing cases
    /// (Coordinates will be fetched from DB). Leave null for NEW personal uploads.
    /// </summary>
    public Guid? AnnotationId { get; set; }
}

public class CitationItemDto
{
    public Guid ChunkId { get; set; }
    public double SimilarityScore { get; set; }
    public string? SourceText { get; set; }
}

public class VisualQAResponseDto
{
    public string? AnswerText { get; set; }
    public string? SuggestedDiagnosis { get; set; }
    public string? DifferentialDiagnoses { get; set; }
    public List<CitationItemDto> Citations { get; set; } = new();
}
