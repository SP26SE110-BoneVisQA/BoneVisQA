using System.Text.Json;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Services.Models.Expert;

namespace BoneVisQA.Services.Helpers;

/// <summary>Builds structured AI report payloads for expert review queue/detail prefill.</summary>
public static class ExpertEscalatedReportBuilder
{
    public static ExpertEscalatedReportDto BuildFromAssistant(QAMessage? assistant)
    {
        if (assistant == null)
            return new ExpertEscalatedReportDto();

        var answerText = VisualQaAssistantAnswerFormatter.FormatDisplayText(assistant);
        var diagnosis = FirstNonEmpty(assistant.SuggestedDiagnosis, answerText);
        var differentialList = DeserializeStringList(assistant.DifferentialDiagnoses);
        var keyFindingsList = SplitMultilineOrJsonList(assistant.KeyImagingFindings);
        var reflectiveList = SplitMultilineOrJsonList(assistant.ReflectiveQuestions);

        return new ExpertEscalatedReportDto
        {
            SuggestedDiagnosis = assistant.SuggestedDiagnosis,
            Diagnosis = diagnosis,
            AnswerText = answerText,
            DifferentialDiagnoses = differentialList,
            KeyFindings = keyFindingsList,
            KeyImagingFindings = FirstNonEmpty(
                assistant.KeyImagingFindings,
                keyFindingsList.Count > 0 ? string.Join("\n", keyFindingsList) : null),
            ReflectiveQuestions = reflectiveList,
            AiConfidenceScore = assistant.AiConfidenceScore
        };
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
