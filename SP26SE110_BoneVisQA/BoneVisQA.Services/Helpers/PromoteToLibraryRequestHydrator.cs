using System.Text.Json;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Services.Models.Expert;
using BoneVisQA.Services.Models.VisualQA;

namespace BoneVisQA.Services.Helpers;

/// <summary>Fills missing promote-to-library fields from session, AI turns, and DICOM metadata.</summary>
public static class PromoteToLibraryRequestHydrator
{
    public static PromoteToLibraryRequestDto Merge(
        PromoteToLibraryRequestDto request,
        VisualQASession session,
        IReadOnlyList<QAMessage> orderedMessages,
        QAMessage? targetUser,
        QAMessage? targetAssistant,
        IReadOnlyList<VisualQaTurnDto> turns,
        JsonElement? dicomMetadata,
        CaseMetadata? sourceCaseMetadata)
    {
        request.Title = FirstNonEmpty(request.Title, session.Case?.Title, "Clinical case from the community")!;
        request.Description = FirstNonEmpty(
            request.Description,
            session.Case?.Description,
            targetAssistant?.Content,
            BuildDescriptionFromAssistant(targetAssistant))!;
        request.SuggestedDiagnosis = FirstNonEmpty(
            request.SuggestedDiagnosis,
            session.Case?.SuggestedDiagnosis,
            targetAssistant?.SuggestedDiagnosis)!;
        request.KeyFindings = FirstNonEmpty(
            request.KeyFindings,
            session.Case?.KeyFindings,
            targetAssistant?.KeyImagingFindings)!;
        request.ReflectiveQuestions = FirstNonEmpty(
            request.ReflectiveQuestions,
            session.Case?.ReflectiveQuestions,
            targetAssistant?.ReflectiveQuestions)!;
        request.ClinicalEvidence = FirstNonEmpty(
            request.ClinicalEvidence,
            request.KeyFindings,
            targetAssistant?.KeyImagingFindings,
            request.Description)!;

        request.Modality = FirstNonEmpty(
            request.Modality,
            sourceCaseMetadata?.Modality,
            CaseMediaDicomMetadataHelper.TryExtractModality(dicomMetadata),
            MapDicomModality(dicomMetadata)) ?? "X-Ray";

        request.AnatomySite = FirstNonEmpty(
            request.AnatomySite,
            sourceCaseMetadata?.AnatomySite,
            CaseMediaDicomMetadataHelper.TryExtractAnatomy(dicomMetadata),
            session.Case?.Category?.Name) ?? "Other";

        request.Laterality = FirstNonEmpty(
            request.Laterality,
            sourceCaseMetadata?.Laterality,
            ReadDicomString(dicomMetadata, "laterality", "Laterality")) ?? "Not-Applicable";

        request.ViewPosition = FirstNonEmpty(
            request.ViewPosition,
            sourceCaseMetadata?.ViewPosition,
            ReadDicomString(dicomMetadata, "view_position", "viewPosition", "ViewPosition")) ?? "AP";

        request.PathologyGroup = FirstNonEmpty(
            request.PathologyGroup,
            sourceCaseMetadata?.PathologyGroup,
            session.Case?.SuggestedDiagnosis) ?? "Trauma";

        request.SourceType = FirstNonEmpty(request.SourceType, sourceCaseMetadata?.SourceType) ?? "Training";

        if (string.IsNullOrWhiteSpace(request.Difficulty))
            request.Difficulty = FirstNonEmpty(sourceCaseMetadata?.Difficulty, session.Case?.Difficulty) ?? "Medium";

        if (request.QualityScore <= 0f && sourceCaseMetadata?.QualityScore is > 0)
            request.QualityScore = (float)sourceCaseMetadata.QualityScore.Value;

        if (request.DifferentialDiagnoses == null || request.DifferentialDiagnoses.Count < 2)
        {
            var fromAssistant = DeserializeStringList(targetAssistant?.DifferentialDiagnoses);
            if (fromAssistant.Count >= 2)
                request.DifferentialDiagnoses = fromAssistant;
            else
            {
                var diagnosis = request.SuggestedDiagnosis?.Trim();
                var alt = session.Case?.Category?.Description?.Trim();
                var list = new List<string>();
                if (!string.IsNullOrWhiteSpace(diagnosis))
                    list.Add(diagnosis);
                if (!string.IsNullOrWhiteSpace(alt) && !list.Contains(alt, StringComparer.OrdinalIgnoreCase))
                    list.Add(alt);
                if (list.Count >= 2)
                    request.DifferentialDiagnoses = list;
            }
        }

        request.TurnAnnotations ??= new List<PromoteCaseAnnotationDto>();
        if (request.TurnAnnotations.Count == 0 && (request.ImageAnnotations == null || request.ImageAnnotations.Count == 0))
        {
            var roiJson = VisualQaRoiResolutionHelper.ResolvePreferredUserRoiJson(
                targetUser,
                session.RequestedReviewMessageId,
                turns);
            if (!string.IsNullOrWhiteSpace(roiJson))
            {
                request.TurnAnnotations.Add(new PromoteCaseAnnotationDto
                {
                    Label = "finding",
                    Coordinates = JsonDocument.Parse(roiJson).RootElement.Clone(),
                });
            }
        }

        HydrateCategory(request, session);

        HydrateTagNamesIfMissing(request);

        return request;
    }

    private static void HydrateCategory(PromoteToLibraryRequestDto request, VisualQASession session)
    {
        if (request.CategoryId is null || request.CategoryId == Guid.Empty)
        {
            if (session.Case?.CategoryId is { } caseCategoryId && caseCategoryId != Guid.Empty)
                request.CategoryId = caseCategoryId;
        }

        if (!string.IsNullOrWhiteSpace(request.CategoryName))
            return;

        request.CategoryName = FirstNonEmpty(
            session.Case?.Category?.Name,
            request.AnatomySite,
            session.TargetBoneSpecialty?.Name,
            request.PathologyGroup,
            ExpertMedicalCaseDisplayHelper.DefaultCategory)!;
    }

    private static void HydrateTagNamesIfMissing(PromoteToLibraryRequestDto request)
    {
        var hasTagNames = request.TagNames is { Count: > 0 } names &&
                          names.Any(t => !string.IsNullOrWhiteSpace(t));
        var hasTagIds = request.TagIds is { Count: > 0 };

        if (hasTagNames || hasTagIds)
            return;

        var autoTags = new List<string>();
        foreach (var candidate in new[]
                 {
                     request.AnatomySite,
                     request.PathologyGroup,
                     request.SuggestedDiagnosis,
                     ExpertMedicalCaseDisplayHelper.DefaultCategory,
                 })
        {
            var trimmed = candidate?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) ||
                autoTags.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
                continue;

            autoTags.Add(trimmed);
            break;
        }

        if (autoTags.Count > 0)
            request.TagNames = autoTags;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }

    private static string? BuildDescriptionFromAssistant(QAMessage? assistant)
    {
        if (assistant == null)
            return null;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(assistant.SuggestedDiagnosis))
            parts.Add(assistant.SuggestedDiagnosis.Trim());
        if (!string.IsNullOrWhiteSpace(assistant.KeyImagingFindings))
            parts.Add(assistant.KeyImagingFindings.Trim());
        return parts.Count == 0 ? null : string.Join("\n\n", parts);
    }

    private static List<string> DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<string>();

        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json);
            return list?
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();
        }
        catch
        {
            return json.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }
    }

    private static string? MapDicomModality(JsonElement? metadata)
    {
        var raw = ReadDicomString(metadata, "modality", "Modality");
        return DicomOntologyMappingHelper.MapDicomModality(raw);
    }

    private static string? ReadDicomString(JsonElement? metadata, params string[] names)
    {
        if (metadata is not { ValueKind: JsonValueKind.Object } root)
            return null;

        foreach (var name in names)
        {
            if (!TryGetProperty(root, name, out var el))
                continue;
            if (el.ValueKind == JsonValueKind.String)
            {
                var s = el.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(s))
                    return s;
            }
        }

        return null;
    }

    private static bool TryGetProperty(JsonElement root, string name, out JsonElement value)
    {
        if (root.TryGetProperty(name, out value))
            return true;

        foreach (var prop in root.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
