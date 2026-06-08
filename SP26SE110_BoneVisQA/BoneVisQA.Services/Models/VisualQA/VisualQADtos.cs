using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using BoneVisQA.Services.Helpers;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Models.VisualQA;

public class VisualQARequestDto
{
    [DefaultValue("Does the highlighted red region in the image show signs of a fracture?")]
    public string QuestionText { get; set; } = string.Empty;

    /// <summary>
    /// Server-populated image location (multipart upload URL, case media, or session hydrate). Not accepted from JSON clients.
    /// </summary>
    [JsonIgnore]
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Normalized bounding box ROI (0–1): JSON <c>{"x":0.1,"y":0.2,"width":0.3,"height":0.4}</c> (also accepts <c>w</c>/<c>h</c>).
    /// Persisted in <c>student_questions.custom_coordinates</c>.
    /// </summary>
    [DefaultValue(null)]
    public string? Coordinates { get; set; }

    /// <summary>
    /// Optional Case ID for library / ingested study context. Omit for personal raster uploads (multipart <c>/ask</c>).
    /// </summary>
    public Guid? CaseId { get; set; }

    /// <summary>
    /// Optional Annotation ID. When set with <see cref="CaseId"/>, coordinates and image are loaded from the catalog annotation.
    /// </summary>
    public Guid? AnnotationId { get; set; }

    /// <summary>
    /// ISO 639-1 tag for Gemini response language, resolved server-side from <c>?locale=</c>, <c>Accept-Language</c>, and Vietnamese question heuristic. Not bound from JSON.
    /// </summary>
    [JsonIgnore]
    public string? ResolvedResponseLanguage { get; set; }

    /// <summary>Optional existing visual QA session id. If null, backend creates/finds by context.</summary>
    public Guid? SessionId { get; set; }

    /// <summary>Optional medical image row id when a case has multiple images (disambiguates study slice).</summary>
    public Guid? ImageId { get; set; }

    /// <summary>Optional FE-generated request id used for optimistic message correlation and future idempotency.</summary>
    public string? ClientRequestId { get; set; }

    /// <summary>
    /// Optional DICOM metadata JSON from upload (<c>dicomMetadata</c>) or case ingest.
    /// When omitted, the server loads <c>case_media.dicom_metadata</c> for <see cref="CaseId"/>.
    /// </summary>
    public JsonElement? DicomMetadata { get; set; }
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
    /// Weighted RAG confidence (0–1): 65% best-chunk similarity + 35% top-3 average; catalog case-study floor 0.55 when metadata is primary ground truth.
    /// Null when unavailable (e.g. embedding failure or generation failure — should be reviewed when possible).
    /// Persisted on <c>qa_messages.ai_confidence_score</c> / <c>case_answers.ai_confidence_score</c>.
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
    /// <summary>Null when unlimited turns are allowed (production/demo default).</summary>
    public int? TurnLimit { get; set; }

    /// <summary>Machine-readable block reason for FE (<c>SESSION_EXPIRED</c>, <c>TURN_LIMIT_EXCEEDED</c>, <c>NO_REVIEW_PATH</c>, …).</summary>
    public string? BlockingReason { get; set; }

    /// <summary>Always <c>lecturer</c> for enrolled students; expert queue is lecturer-driven only.</summary>
    public string ReviewRoute { get; set; } = "none";

    /// <summary><c>personal_dicom</c> or <c>catalog_case_study</c> — separates secondary case-library Q&amp;A from upload flow.</summary>
    public string StudyMode { get; set; } = VisualQaSessionFlowHelper.PersonalDicom;

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

public class VisualQaTurnMessageDto
{
    public string Role { get; set; } = "system";
    public string Content { get; set; } = string.Empty;
    public Guid? MessageId { get; set; }
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
    /// <summary>Same as <see cref="MessageText"/>; many triage UIs expect a flat <c>answerText</c> on each turn.</summary>
    public string? AnswerText { get; set; }
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

    public IReadOnlyList<VisualQaTurnMessageDto> Messages { get; set; } = Array.Empty<VisualQaTurnMessageDto>();

    /// <summary>1-based order within the session thread (Turn #1, #2, …).</summary>
    [JsonPropertyName("turnIndex")]
    public int TurnIndex { get; set; }

    [JsonPropertyName("target_assistant_message_id")]
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

    /// <summary>Catalog teaching case was deleted; session history is read-only context.</summary>
    public bool CaseRemoved { get; set; }

    [JsonPropertyName("mediaId")]
    public Guid? MediaId { get; set; }

    [JsonPropertyName("catalogImageId")]
    public Guid? CatalogImageId { get; set; }

    /// <summary>DICOM tags from the linked catalog case (<c>case_media.dicom_metadata</c>).</summary>
    [JsonPropertyName("dicomMetadata")]
    public JsonElement? DicomMetadata { get; set; }

    public IReadOnlyList<VisualQaTurnDto> Turns { get; set; } = Array.Empty<VisualQaTurnDto>();
    public VisualQaCapabilitiesDto Capabilities { get; set; } = new();
    public string? ReviewState { get; set; }
    public string? LastResponderRole { get; set; }
    public string? BlockingNotice { get; set; }

    /// <summary>When session is <c>Rejected</c>, lecturer/expert rejection text (same source as history <see cref="VisualQaSessionHistoryItemDto.RejectionReason"/>).</summary>
    public string? RejectionReason { get; set; }

    [JsonPropertyName("sessionStatus")]
    public string? SessionStatus { get; set; }

    /// <summary>Review note from lecturer/expert on <c>visual_qa_sessions.review_feedback</c>.</summary>
    [JsonPropertyName("reviewFeedback")]
    public string? ReviewFeedback { get; set; }

    /// <summary>Teaching case created when expert promotes this session to the library.</summary>
    [JsonPropertyName("promotedCaseId")]
    public Guid? PromotedCaseId { get; set; }

    [JsonPropertyName("publishedToLibrary")]
    public bool PublishedToLibrary => PromotedCaseId.HasValue;

    /// <summary>
    /// False when <see cref="SessionId"/> is not an active Visual QA row for this student
    /// (deleted, never created, or stale URL). FE should show empty workspace, not treat as HTTP error.
    /// </summary>
    [JsonPropertyName("sessionExists")]
    public bool SessionExists { get; set; } = true;
}

/// <summary>Session-level review summary for student/history/report views.</summary>
public class VisualQaSessionReportDto
{
    public Guid SessionId { get; set; }

    [JsonPropertyName("sessionStatus")]
    public string SessionStatus { get; set; } = string.Empty;

    [JsonPropertyName("reviewFeedback")]
    public string? ReviewFeedback { get; set; }

    public string? RejectionReason { get; set; }
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

    /// <summary>When <see cref="Status"/> is <c>Rejected</c>, rejection text from <c>review_feedback</c> (legacy fallback: latest lecturer/expert message).</summary>
    public string? RejectionReason { get; set; }

    [JsonPropertyName("sessionStatus")]
    public string SessionStatus { get; set; } = string.Empty;

    [JsonPropertyName("reviewFeedback")]
    public string? ReviewFeedback { get; set; }

    /// <summary><c>personal_dicom</c> or <c>catalog_case_study</c>.</summary>
    public string StudyMode { get; set; } = VisualQaSessionFlowHelper.PersonalDicom;

    /// <summary>True when a catalog case-study session no longer has a linked <see cref="CaseId"/>.</summary>
    public bool CaseRemoved { get; set; }
}

public class PagedResultDto<T>
{
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("items")]
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
}
