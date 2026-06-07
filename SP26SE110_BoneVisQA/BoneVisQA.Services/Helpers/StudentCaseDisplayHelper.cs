using System.Text.Json;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Services.Models.Student;

namespace BoneVisQA.Services.Helpers;

/// <summary>Display fields for student case catalog/detail (location, lesion, expert, promoted Q&amp;A context).</summary>
public static class StudentCaseDisplayHelper
{
    public static string ResolveBoneLocation(MedicalCase entity)
    {
        var fromTags = ExpertMedicalCaseDisplayHelper.ResolveBoneLocationFromTags(entity.CaseTags);
        if (!string.Equals(fromTags, ExpertMedicalCaseDisplayHelper.DefaultBoneLocation, StringComparison.OrdinalIgnoreCase))
            return fromTags;

        var anatomySite = entity.CaseMetadata?.AnatomySite?.Trim();
        if (!string.IsNullOrWhiteSpace(anatomySite))
            return anatomySite;

        var category = entity.Category?.Name?.Trim();
        if (!string.IsNullOrWhiteSpace(category))
            return category;

        return ExpertMedicalCaseDisplayHelper.DefaultBoneLocation;
    }

    public static string ResolveLesionType(MedicalCase entity)
    {
        var fromTags = entity.CaseTags?
            .Where(ct => ct.Tag != null)
            .Where(ct =>
                string.Equals(ct.Tag!.Type, "Lesion Type", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ct.Tag.Type, "Lesion", StringComparison.OrdinalIgnoreCase))
            .Select(ct => ct.Tag!.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(fromTags))
            return fromTags;

        var pathology = entity.CaseMetadata?.PathologyGroup?.Trim();
        if (!string.IsNullOrWhiteSpace(pathology))
            return pathology;

        var category = entity.Category?.Name?.Trim();
        if (!string.IsNullOrWhiteSpace(category))
            return category;

        return ExpertMedicalCaseDisplayHelper.DefaultCategory;
    }

    public static string ResolveExpertName(MedicalCase entity)
    {
        var name = entity.ValidatedByUser?.FullName ?? entity.CreatedByExpert?.FullName;
        return string.IsNullOrWhiteSpace(name)
            ? ExpertMedicalCaseDisplayHelper.DefaultExpertName
            : name.Trim();
    }

    public static bool IsCommunityPromoted(MedicalCase entity) =>
        string.Equals(
            CaseOriginHelper.ResolveStudentCatalogOrigin(entity),
            StudentCaseOriginValues.CommunityPromoted,
            StringComparison.Ordinal);

    public static PromotedCaseContext ParsePromotedContext(MedicalCase entity)
    {
        if (!IsCommunityPromoted(entity) || string.IsNullOrWhiteSpace(entity.CaseMetadata?.ClinicalContext))
            return PromotedCaseContext.Empty;

        try
        {
            using var doc = JsonDocument.Parse(entity.CaseMetadata.ClinicalContext);
            var root = doc.RootElement;

            return new PromotedCaseContext
            {
                StudentQuestion = ReadString(root, "student_question"),
                DifferentialDiagnoses = ReadStringList(root, "differential_diagnoses"),
                ReferencesAndCitations = ReadStringList(root, "references_and_citations", "references"),
            };
        }
        catch (JsonException)
        {
            return PromotedCaseContext.Empty;
        }
    }

    private static string? ReadString(JsonElement root, string name)
    {
        if (!TryGetProperty(root, name, out var el))
            return null;

        return el.ValueKind == JsonValueKind.String
            ? el.GetString()?.Trim()
            : null;
    }

    private static List<string> ReadStringList(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(root, name, out var el))
                continue;

            if (el.ValueKind == JsonValueKind.Array)
            {
                return el.EnumerateArray()
                    .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString()?.Trim() : x.ToString())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x!)
                    .ToList();
            }

            if (el.ValueKind == JsonValueKind.String)
            {
                var raw = el.GetString();
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                return raw
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();
            }
        }

        return new List<string>();
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

    public sealed class PromotedCaseContext
    {
        public static PromotedCaseContext Empty { get; } = new();

        public string? StudentQuestion { get; init; }
        public IReadOnlyList<string> DifferentialDiagnoses { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> ReferencesAndCitations { get; init; } = Array.Empty<string>();
    }
}
