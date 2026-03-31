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

    /// <summary>Optional language hint (e.g. vi, en). Defaults to Vietnamese when null or empty.</summary>
    public string? Language { get; set; }
}

public class CitationItemDto
{
    public Guid ChunkId { get; set; }
    public double SimilarityScore { get; set; }
    /// <summary>
    /// Public URL to the underlying document file stored in Supabase.
    /// </summary>
    public string? DocumentUrl { get; set; }
    /// <summary>
    /// Pseudo-page/order number mapped from document_chunks.chunk_order.
    /// </summary>
    public int ChunkOrder { get; set; }
    public string? SourceText { get; set; }
}

public class VisualQAResponseDto
{
    public string? AnswerText { get; set; }
    public string? SuggestedDiagnosis { get; set; }
    public string? DifferentialDiagnoses { get; set; }
    public List<CitationItemDto> Citations { get; set; } = new();
}
