using System.Text.Json;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Services.Models.VisualQA;

namespace BoneVisQA.Services.Helpers;

public static class VisualQaSessionTurnsMapper
{
    public static IReadOnlyList<VisualQaTurnDto> BuildTurns(
        Guid sessionId,
        IReadOnlyList<QAMessage> orderedMessages,
        string? sessionStatus,
        Guid? requestedReviewMessageId)
    {
        var messages = orderedMessages
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .ToList();

        var reviewState = MapReviewState(sessionStatus);
        var turns = new List<VisualQaTurnDto>();
        QAMessage? pendingUser = null;

        foreach (var message in messages)
        {
            if (string.Equals(message.Role, "User", StringComparison.OrdinalIgnoreCase))
            {
                if (pendingUser != null)
                    turns.Add(MapTurn(sessionId, pendingUser, null, reviewState, requestedReviewMessageId));
                pendingUser = message;
                continue;
            }

            if (string.Equals(message.Role, "Assistant", StringComparison.OrdinalIgnoreCase))
            {
                if (pendingUser != null)
                {
                    turns.Add(MapTurn(sessionId, pendingUser, message, reviewState, requestedReviewMessageId));
                    pendingUser = null;
                }
                else
                {
                    turns.Add(MapStandaloneAssistantTurn(sessionId, message, reviewState, requestedReviewMessageId));
                }
                continue;
            }

            if (pendingUser != null)
            {
                turns.Add(MapTurn(sessionId, pendingUser, null, reviewState, requestedReviewMessageId));
                pendingUser = null;
            }

            if (string.Equals(message.Role, "Lecturer", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(message.Role, "Expert", StringComparison.OrdinalIgnoreCase))
            {
                turns.Add(MapReviewUpdateTurn(sessionId, message));
            }
        }

        if (pendingUser != null)
            turns.Add(MapTurn(sessionId, pendingUser, null, reviewState, requestedReviewMessageId));

        return turns;
    }

    private static string? MapReviewState(string? status)
    {
        return status switch
        {
            "PendingExpertReview" => "pending",
            "EscalatedToExpert" => "escalated",
            "LecturerApproved" => "reviewed",
            "ExpertApproved" => "resolved",
            "Rejected" => "rejected",
            _ => "none"
        };
    }

    private static string TurnReviewState(string? sessionReviewState, bool isReviewTarget) =>
        isReviewTarget ? (sessionReviewState ?? "none") : "none";

    /// <summary>
    /// Raw assistant narrative from <c>qa_messages.content</c> only.
    /// Do not merge <see cref="QAMessage.SuggestedDiagnosis"/> / findings here — those map to <see cref="VisualQaTurnDto"/> structured fields;
    /// duplicating them into <see cref="VisualQaTurnDto.MessageText"/> causes the student UI to show the same text in Markdown and in Diagnosis/Findings cards.
    /// </summary>
    private static string? ResolveAssistantPlainText(QAMessage? m)
    {
        if (m == null)
            return null;

        var body = m.Content?.Trim();
        return string.IsNullOrWhiteSpace(body) ? null : body;
    }

    private static VisualQaTurnDto MapTurn(
        Guid sessionId,
        QAMessage userMessage,
        QAMessage? assistantMessage,
        string? reviewState,
        Guid? requestedReviewMessageId)
    {
        var assistantPlain = ResolveAssistantPlainText(assistantMessage);
        return new VisualQaTurnDto
        {
            SessionId = sessionId,
            TurnId = assistantMessage?.Id.ToString() ?? userMessage.Id.ToString(),
            ActorRole = "assistant",
            UserMessageId = userMessage.Id,
            AssistantMessageId = assistantMessage?.Id,
            UserMessage = userMessage.Content,
            QuestionCoordinates = userMessage.Coordinates,
            QuestionText = userMessage.Content,
            MessageText = assistantPlain,
            AnswerText = assistantPlain,
            Diagnosis = assistantMessage?.SuggestedDiagnosis,
            Findings = SplitMultilineField(assistantMessage?.KeyImagingFindings),
            DifferentialDiagnoses = DeserializeJsonArrayToList(assistantMessage?.DifferentialDiagnoses),
            ReflectiveQuestions = SplitMultilineField(assistantMessage?.ReflectiveQuestions),
            Citations = ResolveMessageCitations(assistantMessage),
            CreatedAt = userMessage.CreatedAt,
            ResponseKind = DetermineResponseKind(assistantMessage),
            PolicyReason = DeterminePolicyReason(assistantMessage),
            ReviewState = TurnReviewState(
                reviewState,
                assistantMessage != null && requestedReviewMessageId.HasValue &&
                assistantMessage.Id == requestedReviewMessageId.Value),
            LastResponderRole = assistantMessage == null ? "system" : "assistant",
            IsReviewTarget = assistantMessage != null && requestedReviewMessageId.HasValue &&
                             assistantMessage.Id == requestedReviewMessageId.Value
        };
    }

    private static VisualQaTurnDto MapStandaloneAssistantTurn(
        Guid sessionId,
        QAMessage assistantMessage,
        string? reviewState,
        Guid? requestedReviewMessageId)
    {
        var assistantPlain = ResolveAssistantPlainText(assistantMessage);
        return new VisualQaTurnDto
        {
            SessionId = sessionId,
            TurnId = assistantMessage.Id.ToString(),
            ActorRole = "assistant",
            UserMessageId = Guid.Empty,
            AssistantMessageId = assistantMessage.Id,
            UserMessage = string.Empty,
            QuestionText = null,
            MessageText = assistantPlain,
            AnswerText = assistantPlain,
            Diagnosis = assistantMessage.SuggestedDiagnosis,
            Findings = SplitMultilineField(assistantMessage.KeyImagingFindings),
            DifferentialDiagnoses = DeserializeJsonArrayToList(assistantMessage.DifferentialDiagnoses),
            ReflectiveQuestions = SplitMultilineField(assistantMessage.ReflectiveQuestions),
            Citations = ResolveMessageCitations(assistantMessage),
            CreatedAt = assistantMessage.CreatedAt,
            ResponseKind = DetermineResponseKind(assistantMessage),
            PolicyReason = DeterminePolicyReason(assistantMessage),
            ReviewState = TurnReviewState(
                reviewState,
                requestedReviewMessageId.HasValue && assistantMessage.Id == requestedReviewMessageId.Value),
            LastResponderRole = "assistant",
            IsReviewTarget = requestedReviewMessageId.HasValue && assistantMessage.Id == requestedReviewMessageId.Value
        };
    }

    private static VisualQaTurnDto MapReviewUpdateTurn(Guid sessionId, QAMessage message)
    {
        var actorRole = MapResponderRole(message.Role) ?? "system";
        var (targetAssistantId, displayContent) = VisualQaReviewFeedbackRouting.Resolve(message);
        return new VisualQaTurnDto
        {
            SessionId = sessionId,
            TurnId = message.Id.ToString(),
            ActorRole = actorRole,
            UserMessageId = Guid.Empty,
            // Must be this row's id — not the targeted AI assistant id — or clients merge with the AI turn and show wrong policyReason (e.g. medical_intent).
            AssistantMessageId = message.Id,
            UserMessage = string.Empty,
            QuestionText = null,
            MessageText = displayContent,
            AnswerText = displayContent,
            TargetAssistantMessageId = targetAssistantId,
            Diagnosis = message.SuggestedDiagnosis,
            Findings = SplitMultilineField(message.KeyImagingFindings),
            DifferentialDiagnoses = DeserializeJsonArrayToList(message.DifferentialDiagnoses),
            ReflectiveQuestions = SplitMultilineField(message.ReflectiveQuestions),
            Citations = ResolveMessageCitations(message),
            CreatedAt = message.CreatedAt,
            ResponseKind = "review_update",
            PolicyReason = null,
            ReviewState = "none",
            LastResponderRole = actorRole,
            IsReviewTarget = false
        };
    }

    private static IReadOnlyList<CitationItemDto> ResolveMessageCitations(QAMessage? message)
    {
        if (message == null)
            return Array.Empty<CitationItemDto>();

        var fromJson = VisualQaCitationMetadataBuilder.DeserializeMany(message.CitationsJson);
        if (fromJson.Count > 0)
            return fromJson
                .Take(5)
                .ToList();

        return MapTurnCitations(message.Citations);
    }

    private static IReadOnlyList<CitationItemDto> MapTurnCitations(ICollection<Citation>? citations)
    {
        if (citations == null || citations.Count == 0)
            return Array.Empty<CitationItemDto>();

        return citations
            .OrderBy(c => c.Chunk?.ChunkOrder ?? int.MaxValue)
            .ThenBy(c => c.Id)
            .Select(c => VisualQaCitationMetadataBuilder.FromDocumentChunk(c.Chunk))
            .Take(5)
            .ToList();
    }

    private static IReadOnlyList<string> SplitMultilineField(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        return raw
            .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim().TrimStart('-', '*').Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    private static IReadOnlyList<string> DeserializeJsonArrayToList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<string>();

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(json);
            if (parsed == null || parsed.Count == 0)
                return Array.Empty<string>();

            return parsed
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList();
        }
        catch
        {
            return SplitMultilineField(json);
        }
    }

    private const string GeminiNoContextAnswer =
        "The current medical data does not contain enough information to answer this question.";
    private const string GeminiFallbackNoReliableInfoAnswer =
        "Sorry, based on our musculoskeletal medical knowledge base, I could not find sufficiently reliable information to answer this advanced question.";

    private static string DetermineResponseKind(QAMessage? assistantMessage)
    {
        if (assistantMessage == null)
            return "clarification";

        if (string.Equals(assistantMessage.Role, "Lecturer", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(assistantMessage.Role, "Expert", StringComparison.OrdinalIgnoreCase))
            return "review_update";

        var diagnosis = (assistantMessage.SuggestedDiagnosis ?? assistantMessage.Content ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(diagnosis))
            return "clarification";

        if (diagnosis.Contains("not related to the musculoskeletal medical domain", StringComparison.OrdinalIgnoreCase) ||
            diagnosis.Contains("not valid medical data", StringComparison.OrdinalIgnoreCase) ||
            diagnosis.Contains("not a valid human bone x-ray image", StringComparison.OrdinalIgnoreCase) ||
            diagnosis.Contains("please upload a proper medical x-ray image", StringComparison.OrdinalIgnoreCase))
            return "refusal";

        if (string.Equals(diagnosis, GeminiNoContextAnswer, StringComparison.Ordinal) ||
            string.Equals(diagnosis, GeminiFallbackNoReliableInfoAnswer, StringComparison.Ordinal))
            return "clarification";

        var findings = SplitMultilineField(assistantMessage.KeyImagingFindings);
        var differentialDiagnoses = DeserializeJsonArrayToList(assistantMessage.DifferentialDiagnoses);
        var reflectiveQuestions = SplitMultilineField(assistantMessage.ReflectiveQuestions);

        // Chỉ có SuggestedDiagnosis (DTO Diagnosis) mà không có findings/differential/reflective
        // vẫn là analysis — tránh FE nhận clarification và lệch nhánh hiển thị.
        var hasSuggestedDiagnosis = !string.IsNullOrWhiteSpace(assistantMessage.SuggestedDiagnosis);
        var hasClinicalListings =
            findings.Count > 0 || differentialDiagnoses.Count > 0 || reflectiveQuestions.Count > 0;

        if (hasSuggestedDiagnosis || hasClinicalListings)
            return "analysis";

        return "clarification";
    }

    private static string? DeterminePolicyReason(QAMessage? assistantMessage)
    {
        if (assistantMessage == null)
            return "clarification";

        var responseKind = DetermineResponseKind(assistantMessage);
        if (string.Equals(responseKind, "refusal", StringComparison.Ordinal))
            return "off_topic";

        if (string.Equals(responseKind, "review_update", StringComparison.Ordinal))
            return "review_update";

        return "medical_intent";
    }

    private static string? MapResponderRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return null;

        return role.Trim() switch
        {
            "Assistant" => "assistant",
            "Lecturer" => "lecturer",
            "Expert" => "expert",
            _ => "system"
        };
    }
}
