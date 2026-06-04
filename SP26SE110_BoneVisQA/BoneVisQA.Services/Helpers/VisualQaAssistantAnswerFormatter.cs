using System.Text;
using System.Text.Json;
using BoneVisQA.Repositories.Models;

namespace BoneVisQA.Services.Helpers;

/// <summary>
/// Builds human-readable AI answer text from <c>qa_messages</c> when <see cref="QAMessage.Content"/> is empty
/// but structured Gemini fields (<see cref="QAMessage.SuggestedDiagnosis"/>, etc.) are populated.
/// </summary>
public static class VisualQaAssistantAnswerFormatter
{
    public static string? FormatDisplayText(QAMessage? message)
    {
        if (message == null)
            return null;

        var narrative = message.Content?.Trim();
        if (!string.IsNullOrWhiteSpace(narrative))
            return narrative;

        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(message.SuggestedDiagnosis))
            sb.AppendLine(message.SuggestedDiagnosis.Trim());

        var findings = SplitMultilineOrJsonList(message.KeyImagingFindings);
        if (findings.Count > 0)
        {
            if (sb.Length > 0)
                sb.AppendLine();
            sb.AppendLine("Key imaging findings:");
            foreach (var item in findings)
                sb.AppendLine($"- {item}");
        }

        var differentials = DeserializeStringList(message.DifferentialDiagnoses);
        if (differentials.Count > 0)
        {
            if (sb.Length > 0)
                sb.AppendLine();
            sb.AppendLine("Differential diagnoses:");
            foreach (var item in differentials)
                sb.AppendLine($"- {item}");
        }

        var reflective = SplitMultilineOrJsonList(message.ReflectiveQuestions);
        if (reflective.Count > 0)
        {
            if (sb.Length > 0)
                sb.AppendLine();
            sb.AppendLine("Reflective questions:");
            foreach (var item in reflective)
                sb.AppendLine($"- {item}");
        }

        var formatted = sb.ToString().Trim();
        return formatted.Length == 0 ? null : formatted;
    }

    private static IReadOnlyList<string> SplitMultilineOrJsonList(string? raw)
    {
        var fromJson = DeserializeStringList(raw);
        if (fromJson.Count > 0)
            return fromJson;

        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        return raw
            .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim().TrimStart('-', '*').Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    private static List<string> DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<string>();

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(json);
            return parsed?
                       .Where(x => !string.IsNullOrWhiteSpace(x))
                       .Select(x => x.Trim())
                       .ToList()
                   ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }
}
