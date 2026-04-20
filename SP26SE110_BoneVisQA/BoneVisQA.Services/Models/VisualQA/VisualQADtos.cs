using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Models.VisualQA;

public class VisualQARequestDto
{
    [DefaultValue("Does the highlighted red region in the image show signs of a fracture?")]
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

    /// <summary>
    /// Optional ISO 639-1 language tag (e.g. <c>vi</c>, <c>en</c>). The API merges this with query <c>locale</c>,
    /// multipart field <c>language</c>, <c>Accept-Language</c>, then defaults to Vietnamese.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>Optional existing visual QA session id. If null, backend creates/finds by context.</summary>
    public Guid? SessionId { get; set; }

    /// <summary>Optional image id for disambiguating image inside a medical case.</summary>
    public Guid? ImageId { get; set; }

    /// <summary>Optional FE-generated request id used for optimistic message correlation and future idempotency.</summary>
    public string? ClientRequestId { get; set; }
}

public class CitationItemDto
{
    public Guid ChunkId { get; set; }

    /// <summary>Set when the citation comes from <c>medical_cases</c> RAG (not a document chunk).</summary>
    public Guid? MedicalCaseId { get; set; }

    /// <summary>
    /// Public URL to the underlying document file stored in Supabase.
    /// </summary>
    public string? ReferenceUrl { get; set; }
    /// <summary>
    /// Best-effort page hint derived from `document_chunks.chunk_order` when true page metadata is unavailable.
    /// </summary>
    public int? PageNumber { get; set; }
    public int? StartPage { get; set; }
    public int? EndPage { get; set; }
    public string? SourceText { get; set; }
    public string? DisplayLabel { get; set; }
    public string? PageLabel { get; set; }
    public string? Href { get; set; }
    public string? Snippet { get; set; }
    public string Kind { get; set; } = "doc";
}

public class VisualQAResponseDto
{
    public Guid? SessionId { get; set; }
    public string? TurnId { get; set; }
    public string? UserQuestionText { get; set; }
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
    public string ResponseKind { get; set; } = "analysis";
    public string? PolicyReason { get; set; }
    public string? ClientRequestId { get; set; }

    /// <summary>Catalog case when non-null; null or absent for personal uploads.</summary>
    public Guid? CaseId { get; set; }

    public List<CitationItemDto> Citations { get; set; } = new();
}

/// <summary>Streaming Visual QA: token/text deltas plus the same finalized <see cref="VisualQAResponseDto"/> as non-streaming after the model finishes.</summary>
public sealed class VisualQaStreamingPipelineResult
{
    public IAsyncEnumerable<string> TextDeltas { get; init; } = default!;
    public Task<VisualQAResponseDto> CompletedResponseAsync { get; init; } = default!;
}

public class VisualQaCapabilitiesDto
{
    public bool CanAskNext { get; set; }
    public bool IsReadOnly { get; set; }
    public bool CanRequestReview { get; set; }
    public int TurnsUsed { get; set; }
    public int TurnLimit { get; set; }
    [JsonIgnore]
    public string? Reason { get; set; }
}

public class VisualQaApiResponseDto
{
    public Guid? SessionId { get; set; }

    /// <summary>Catalog case when non-null; null for personal uploads.</summary>
    public Guid? CaseId { get; set; }

    public bool IsPersonalUpload => !CaseId.HasValue;

    public string Diagnosis { get; set; } = string.Empty;
    public IReadOnlyList<string> Findings { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> DifferentialDiagnoses { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ReflectiveQuestions { get; set; } = Array.Empty<string>();
    public IReadOnlyList<CitationItemDto> Citations { get; set; } = Array.Empty<CitationItemDto>();
    public VisualQaCapabilitiesDto Capabilities { get; set; } = new();
    public string ResponseKind { get; set; } = "analysis";
    public string? PolicyReason { get; set; }
    public string? ClientRequestId { get; set; }
    public string? ReviewState { get; set; }
    public string? LastResponderRole { get; set; }
    public string? SystemNotice { get; set; }
    [JsonIgnore]
    public string? SystemNoticeCode { get; set; }
    public VisualQaTurnDto? LatestTurn { get; set; }
}

public class VisualQaTurnDto
{
    public Guid SessionId { get; set; }
    public string? TurnId { get; set; }
    public string ActorRole { get; set; } = "assistant";
    public Guid UserMessageId { get; set; }
    public Guid? AssistantMessageId { get; set; }
    public string UserMessage { get; set; } = string.Empty;
    /// <summary>ROI / bbox JSON from <c>qa_messages.coordinates</c> on the user message (normalized 0–1 when stored that way).</summary>
    public string? QuestionCoordinates { get; set; }
    public string? QuestionText { get; set; }
    public string? MessageText { get; set; }
    public string? Diagnosis { get; set; }
    public IReadOnlyList<string> Findings { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> DifferentialDiagnoses { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ReflectiveQuestions { get; set; } = Array.Empty<string>();
    public IReadOnlyList<CitationItemDto> Citations { get; set; } = Array.Empty<CitationItemDto>();
    public DateTime CreatedAt { get; set; }
    public string ResponseKind { get; set; } = "analysis";
    public string? PolicyReason { get; set; }
    public string? ReviewState { get; set; }
    public string? LastResponderRole { get; set; }
    public bool IsReviewTarget { get; set; }

    public Guid? TargetAssistantMessageId { get; set; }
}

public class VisualQaThreadDto
{
    public Guid SessionId { get; set; }
    /// <summary>Resolved study image (signed when required). Aligns with history list <see cref="VisualQaSessionHistoryItemDto.ImageUrl"/>.</summary>
    public string? SessionImageUrl { get; set; }
    /// <summary>Same as <see cref="SessionImageUrl"/> (JSON name <c>imageUrl</c>) for clients that reuse list-row field naming.</summary>
    public string? ImageUrl { get; set; }
    /// <summary>Same as <see cref="SessionImageUrl"/> (JSON name <c>studyImageUrl</c>) for Visual QA page prefill / query symmetry.</summary>
    public string? StudyImageUrl { get; set; }
    /// <summary>Primary ROI JSON for the viewer (latest user message with coordinates in this session).</summary>
    public string? RoiBoundingBox { get; set; }
    public Guid? CaseId { get; set; }
    public Guid? ImageId { get; set; }
    public IReadOnlyList<VisualQaTurnDto> Turns { get; set; } = Array.Empty<VisualQaTurnDto>();
    public VisualQaCapabilitiesDto Capabilities { get; set; } = new();
    public string? ReviewState { get; set; }
    public string? LastResponderRole { get; set; }
    public string? BlockingNotice { get; set; }
}

/// <summary>Summary row for Visual QA session history (student).</summary>
public class VisualQaSessionHistoryItemDto
{
    public Guid SessionId { get; set; }
    public Guid? CaseId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? ImageUrl { get; set; }
    /// <summary>First user question in the session (truncated for list views).</summary>
    public string? QuestionSnippet { get; set; }
    public string? ReviewState { get; set; }
    public string? LastResponderRole { get; set; }

    /// <summary>When <see cref="Status"/> is <c>Rejected</c>, latest lecturer message content (rejection reason).</summary>
    public string? RejectionReason { get; set; }
}

public class PagedResultDto<T>
{
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("items")]
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
}
