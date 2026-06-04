using System.Globalization;
using System.Text;
using System.Text.Json;

namespace BoneVisQA.Services.Helpers;

/// <summary>Formats <c>case_media.dicom_metadata</c> JSON for Gemini / RAG clinical context blocks.</summary>
public static class DicomClinicalContextHelper
{
    private static readonly JsonSerializerOptions JsonRead = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static JsonElement? TryParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static JsonElement? Coalesce(JsonElement? primary, JsonElement? fallback) =>
        primary is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined } ? primary : fallback;

    /// <summary>Human-readable block injected into LLM prompts.</summary>
    public static string? BuildPromptBlock(JsonElement? metadata)
    {
        if (metadata is not { } root || root.ValueKind != JsonValueKind.Object)
            return null;

        var modality = ReadString(root, "modality");
        var bodyPart = ReadString(root, "body_part_examined", "bodyPartExamined", "anatomy_site", "anatomySite");
        var patientAge = ReadString(root, "patient_age", "patientAge");
        var patientSex = ReadString(root, "patient_sex", "patientSex");
        var sliceThickness = ReadDouble(root, "slice_thickness", "sliceThickness");
        var studyDesc = ReadString(root, "study_description", "studyDescription");
        var seriesDesc = ReadString(root, "series_description", "seriesDescription");
        var laterality = ReadString(root, "laterality");
        var viewPosition = ReadString(root, "view_position", "viewPosition");

        if (modality == null && bodyPart == null && patientAge == null && patientSex == null
            && sliceThickness == null && studyDesc == null && seriesDesc == null)
            return null;

        var sb = new StringBuilder();
        sb.AppendLine("CLINICAL CONTEXT FROM DICOM (use to improve diagnostic accuracy; do not invent values not listed here):");
        if (!string.IsNullOrWhiteSpace(modality))
            sb.AppendLine($"- Modality: {modality}");
        if (!string.IsNullOrWhiteSpace(bodyPart))
            sb.AppendLine($"- Body part / anatomy: {bodyPart}");
        if (!string.IsNullOrWhiteSpace(patientAge) || !string.IsNullOrWhiteSpace(patientSex))
        {
            sb.Append("- Patient age / sex: ");
            sb.Append(string.IsNullOrWhiteSpace(patientAge) ? "N/A" : patientAge);
            sb.Append(" / ");
            sb.AppendLine(string.IsNullOrWhiteSpace(patientSex) ? "N/A" : patientSex);
        }
        if (sliceThickness.HasValue)
            sb.AppendLine($"- Slice thickness: {sliceThickness.Value.ToString("0.###", CultureInfo.InvariantCulture)} mm");
        if (!string.IsNullOrWhiteSpace(laterality))
            sb.AppendLine($"- Laterality: {laterality}");
        if (!string.IsNullOrWhiteSpace(viewPosition))
            sb.AppendLine($"- View position: {viewPosition}");
        if (!string.IsNullOrWhiteSpace(studyDesc))
            sb.AppendLine($"- Study description: {studyDesc}");
        if (!string.IsNullOrWhiteSpace(seriesDesc))
            sb.AppendLine($"- Series description: {seriesDesc}");

        return sb.ToString().TrimEnd();
    }

    private static string? ReadString(JsonElement root, params string[] names)
    {
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
            else if (el.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
            {
                return el.ToString();
            }
        }

        return null;
    }

    private static double? ReadDouble(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(root, name, out var el))
                continue;
            if (el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out var d))
                return d;
            if (el.ValueKind == JsonValueKind.String
                && double.TryParse(el.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
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
