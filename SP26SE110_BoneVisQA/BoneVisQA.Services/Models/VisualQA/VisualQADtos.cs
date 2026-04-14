using System;
using System.ComponentModel;

namespace BoneVisQA.Services.Models.VisualQA;

public class VisualQARequestDto
{
    [DefaultValue("Vùng khoanh đỏ trên ảnh có dấu hiệu gãy xương không?")]
    public string QuestionText { get; set; } = string.Empty;

    [DefaultValue(null)]
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Normalized bounding box ROI (0–1): JSON <c>{"x":0.1,"y":0.2,"width":0.3,"height":0.4}</c> (also accepts <c>w</c>/<c>h</c>).
    /// Persisted in <c>student_questions.custom_coordinates</c>.
    /// </summary>
    [DefaultValue(null)]
    public string? Coordinates { get; set; }

    /// <summary>
    /// Optional Case ID. Leave null for NEW personal uploads.
    /// </summary>
    public Guid? CaseId { get; set; }

    /// <summary>
    /// Optional Annotation ID. Provide for inquiries on existing cases
    /// (bounding box will be fetched from DB). Leave null for NEW personal uploads.
    /// </summary>
    public Guid? AnnotationId { get; set; }

    /// <summary>Optional language hint (e.g. vi, en). Defaults to Vietnamese when null or empty.</summary>
    public string? Language { get; set; }

    /// <summary>Optional existing visual QA session id. If null, backend creates/finds by context.</summary>
    public Guid? SessionId { get; set; }

    /// <summary>Optional image id for disambiguating image inside a medical case.</summary>
    public Guid? ImageId { get; set; }
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
    public Guid? SessionId { get; set; }
    public string? AnswerText { get; set; }
    public string? SuggestedDiagnosis { get; set; }
    public List<string>? DifferentialDiagnoses { get; set; }
    /// <summary>Key imaging signs to focus on (SEPS).</summary>
    public string? KeyImagingFindings { get; set; }
    /// <summary>Reflective questions for student self-assessment (SEPS).</summary>
    public string? ReflectiveQuestions { get; set; }
    /// <summary>
    /// Best cosine similarity (0–1) between the query embedding and retrieved chunks before generation.
    /// Null when unavailable (e.g. embedding failure or generation failure — should be reviewed when possible).
    /// Persisted on <c>case_answers.ai_confidence_score</c>.
    /// </summary>
    public double? AiConfidenceScore { get; set; }

    /// <summary>Optional client-facing explanation when the AI pipeline failed after retries (not persisted).</summary>
    public string? ErrorMessage { get; set; }

    public List<CitationItemDto> Citations { get; set; } = new();
}
