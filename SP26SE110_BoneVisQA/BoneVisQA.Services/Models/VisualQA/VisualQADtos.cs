using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace BoneVisQA.Services.Models.VisualQA;

/// <summary>Single vertex for polygon lesion annotations (normalized, percent, or pixel — see API contract).</summary>
public class PointDto
{
    public double X { get; set; }
    public double Y { get; set; }
}

public class VisualQARequestDto
{
    [DefaultValue("Vùng khoanh đỏ trên ảnh có dấu hiệu gãy xương không?")]
    public string QuestionText { get; set; } = string.Empty;

    [DefaultValue(null)]
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Lesion outline as a closed polygon (≥3 points). Preferred over <see cref="Coordinates"/>.
    /// Persisted as JSON in <c>student_questions.custom_coordinates</c>.
    /// </summary>
    public List<PointDto>? CustomPolygon { get; set; }

    /// <summary>
    /// Legacy rectangular ROI: JSON <c>{"x","y","w","h"}</c>. Used only when <see cref="CustomPolygon"/> is null/empty.
    /// </summary>
    [DefaultValue(null)]
    public string? Coordinates { get; set; }

    /// <summary>
    /// Optional Case ID. Leave null for NEW personal uploads.
    /// </summary>
    public Guid? CaseId { get; set; }

    /// <summary>
    /// Optional Annotation ID. Provide for inquiries on existing cases
    /// (polygon/box will be fetched from DB). Leave null for NEW personal uploads.
    /// </summary>
    public Guid? AnnotationId { get; set; }

    /// <summary>Optional language hint (e.g. vi, en). Defaults to Vietnamese when null or empty.</summary>
    public string? Language { get; set; }
}

public class CitationItemDto
{
    public Guid ChunkId { get; set; }
    /// <summary>
    /// Public URL to the underlying document file stored in Supabase.
    /// </summary>
    public string? ReferenceUrl { get; set; }
    /// <summary>
    /// Best-effort page hint derived from `document_chunks.chunk_order` when true page metadata is unavailable.
    /// </summary>
    public int? PageNumber { get; set; }
    public string? SourceText { get; set; }
}

public class VisualQAResponseDto
{
    public string? AnswerText { get; set; }
    public string? SuggestedDiagnosis { get; set; }
    public string? DifferentialDiagnoses { get; set; }
    /// <summary>Key imaging signs to focus on (SEPS).</summary>
    public string? KeyImagingFindings { get; set; }
    /// <summary>Reflective questions for student self-assessment (SEPS).</summary>
    public string? ReflectiveQuestions { get; set; }
    public List<CitationItemDto> Citations { get; set; } = new();
}
