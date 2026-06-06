using System.Linq;
using System.Text.Json;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Services.Models.VisualQA;

namespace BoneVisQA.Services.Helpers;

public static class CaseMediaDicomMetadataHelper
{
    public static CaseMedia? ResolveFirstMedia(MedicalCase? medicalCase)
    {
        return medicalCase?.CaseMedia?
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .FirstOrDefault();
    }

    public static Guid? ResolveFirstMediaId(MedicalCase? medicalCase) => ResolveFirstMedia(medicalCase)?.Id;

    public static Guid? ResolveFirstCatalogImageId(MedicalCase? medicalCase)
    {
        return medicalCase?.MedicalImages?
            .OrderBy(m => m.CreatedAt ?? DateTime.MinValue)
            .ThenBy(m => m.Id)
            .Select(m => m.Id)
            .FirstOrDefault();
    }

    /// <summary>Preview URL for catalog / ingest cases: <c>medical_images</c> first, then <c>case_media</c>.</summary>
    public static string? ResolveFirstPreviewUrl(MedicalCase? medicalCase)
    {
        if (medicalCase == null)
            return null;

        var fromCatalogImage = medicalCase.MedicalImages?
            .OrderBy(m => m.CreatedAt ?? DateTime.MinValue)
            .ThenBy(m => m.Id)
            .Select(m => m.ImageUrl)
            .FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));
        if (!string.IsNullOrWhiteSpace(fromCatalogImage))
            return fromCatalogImage.Trim();

        var media = ResolveFirstMedia(medicalCase);
        if (media == null)
            return null;

        if (!string.IsNullOrWhiteSpace(media.MediaUrl))
            return media.MediaUrl.Trim();

        return string.IsNullOrWhiteSpace(media.StoragePath) ? null : media.StoragePath.Trim();
    }

    public static JsonElement? ResolveFirstMetadata(MedicalCase? medicalCase)
    {
        var json = medicalCase?.CaseMedia?
            .Where(m => !string.IsNullOrWhiteSpace(m.DicomMetadata))
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .Select(m => m.DicomMetadata)
            .FirstOrDefault();

        return DicomClinicalContextHelper.TryParseJson(json);
    }

    public static string? TryExtractModality(JsonElement? metadata) =>
        ReadMetadataString(metadata, "modality", "Modality");

    public static string? TryExtractAnatomy(JsonElement? metadata) =>
        ReadMetadataString(metadata,
            "anatomy_site", "anatomySite", "AnatomySite",
            "body_part_examined", "bodyPartExamined", "BodyPartExamined",
            "anatomy", "Anatomy");

    public static string? TryExtractFindings(JsonElement? metadata) =>
        ReadMetadataString(metadata,
            "key_findings", "keyFindings", "KeyFindings",
            "findings", "Findings",
            "study_description", "studyDescription", "StudyDescription");

    private static string? ReadMetadataString(JsonElement? metadata, params string[] propertyNames)
    {
        if (metadata is not { ValueKind: JsonValueKind.Object } root)
            return null;

        foreach (var name in propertyNames)
        {
            if (!TryGetProperty(root, name, out var el))
                continue;

            if (el.ValueKind == JsonValueKind.String)
            {
                var s = el.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(s))
                    return s;
            }
            else if (el.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
            {
                return el.ToString();
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

    public static void ApplyCatalogStudyContext(VisualQARequestDto request, MedicalCase? medicalCase)
    {
        if (medicalCase == null)
            return;

        request.DicomMetadata ??= ResolveFirstMetadata(medicalCase);

        if (!request.ImageId.HasValue || request.ImageId.Value == Guid.Empty)
            request.ImageId = ResolveFirstCatalogImageId(medicalCase);

        if (string.IsNullOrWhiteSpace(request.ImageUrl))
            request.ImageUrl = ResolveFirstPreviewUrl(medicalCase);
    }
}
