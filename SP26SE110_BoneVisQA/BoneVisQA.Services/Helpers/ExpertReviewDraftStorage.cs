using System.Text.Json;
using System.Text.Json.Serialization;
using BoneVisQA.Services.Models.Expert;

namespace BoneVisQA.Services.Helpers;

/// <summary>Persists in-progress expert review edits inside <c>expert_reviews.review_note</c> (JSON marker).</summary>
public static class ExpertReviewDraftStorage
{
    private const string DraftPrefix = "__expert_draft_v1__:";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public sealed class DraftPayload
    {
        public string? ReviewNote { get; set; }
        public string? AnswerText { get; set; }
        public string? StructuredDiagnosis { get; set; }
        public List<string>? DifferentialDiagnoses { get; set; }
        public string? KeyImagingFindings { get; set; }
        public string? ReflectiveQuestions { get; set; }
        public double[]? CorrectedRoiBoundingBox { get; set; }
    }

    public static bool HasAnyContent(ExpertVisualSessionDraftRequestDto request)
    {
        if (request == null)
            return false;

        return !string.IsNullOrWhiteSpace(request.ReviewNote)
               || !string.IsNullOrWhiteSpace(request.AnswerText)
               || !string.IsNullOrWhiteSpace(request.StructuredDiagnosis)
               || !string.IsNullOrWhiteSpace(request.KeyImagingFindings)
               || !string.IsNullOrWhiteSpace(request.ReflectiveQuestions)
               || HasDifferentialDiagnoses(request.DifferentialDiagnoses)
               || (request.CorrectedRoiBoundingBox != null && request.CorrectedRoiBoundingBox.Length >= 4);
    }

    public static string Serialize(ExpertVisualSessionDraftRequestDto request)
    {
        var payload = new DraftPayload
        {
            ReviewNote = TrimOrNull(request.ReviewNote),
            AnswerText = TrimOrNull(request.AnswerText),
            StructuredDiagnosis = TrimOrNull(request.StructuredDiagnosis),
            KeyImagingFindings = TrimOrNull(request.KeyImagingFindings),
            ReflectiveQuestions = TrimOrNull(request.ReflectiveQuestions),
            DifferentialDiagnoses = ParseDifferentialList(request.DifferentialDiagnoses),
            CorrectedRoiBoundingBox = request.CorrectedRoiBoundingBox is { Length: >= 4 }
                ? request.CorrectedRoiBoundingBox
                : null
        };

        return DraftPrefix + JsonSerializer.Serialize(payload, JsonOptions);
    }

    public static bool TryDeserialize(string? reviewNote, out DraftPayload payload)
    {
        payload = new DraftPayload();
        if (string.IsNullOrWhiteSpace(reviewNote) || !reviewNote.StartsWith(DraftPrefix, StringComparison.Ordinal))
            return false;

        try
        {
            var json = reviewNote[DraftPrefix.Length..];
            payload = JsonSerializer.Deserialize<DraftPayload>(json, JsonOptions) ?? new DraftPayload();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string? ExtractHumanReviewNote(string? reviewNote)
    {
        if (TryDeserialize(reviewNote, out var payload))
            return payload.ReviewNote;

        return string.IsNullOrWhiteSpace(reviewNote) ? null : reviewNote.Trim();
    }

    private static bool HasDifferentialDiagnoses(System.Text.Json.JsonElement? element)
    {
        if (element is not { ValueKind: not System.Text.Json.JsonValueKind.Null and not System.Text.Json.JsonValueKind.Undefined })
            return false;

        return element.Value.ValueKind switch
        {
            System.Text.Json.JsonValueKind.Array => element.Value.GetArrayLength() > 0,
            System.Text.Json.JsonValueKind.String => !string.IsNullOrWhiteSpace(element.Value.GetString()),
            _ => false
        };
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static List<string>? ParseDifferentialList(System.Text.Json.JsonElement? element)
    {
        if (element is not { ValueKind: not System.Text.Json.JsonValueKind.Null and not System.Text.Json.JsonValueKind.Undefined })
            return null;

        try
        {
            if (element.Value.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                return element.Value.EnumerateArray()
                    .Select(x => x.ValueKind == System.Text.Json.JsonValueKind.String ? x.GetString() : x.ToString())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            if (element.Value.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var raw = element.Value.GetString();
                if (string.IsNullOrWhiteSpace(raw))
                    return null;

                return raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            }
        }
        catch
        {
            return null;
        }

        return null;
    }
}
