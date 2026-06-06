using System;
using System.Text.RegularExpressions;

namespace BoneVisQA.Services.Helpers;

/// <summary>Guards educator feedback fields from AI structured answer leakage.</summary>
public static class VisualQaEducatorFeedbackHelper
{
    private static readonly Regex StructuredAiBlockRegex = new(
        @"(?i)(?:^|\n)\s*(?:diagnosis|findings|differential|reflective\s*questions?|key\s*imaging)\s*:",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool IsAwaitingHumanReview(string? sessionStatus)
    {
        if (string.IsNullOrWhiteSpace(sessionStatus))
            return false;

        return sessionStatus.Trim() switch
        {
            "PendingExpertReview" or "EscalatedToExpert" => true,
            _ => false
        };
    }

    public static bool IsTerminalReview(string? sessionStatus)
    {
        if (string.IsNullOrWhiteSpace(sessionStatus))
            return false;

        return sessionStatus.Trim() switch
        {
            "LecturerApproved" or "ExpertApproved" or "Rejected" => true,
            _ => false
        };
    }

    public static bool IsLikelyAiStructuredBlock(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.Trim();
        if (StructuredAiBlockRegex.IsMatch(trimmed))
            return true;

        var lineCount = trimmed.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
        return lineCount >= 3 &&
               (trimmed.Contains("Differential", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("Key imaging", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("Reflective", StringComparison.OrdinalIgnoreCase));
    }

    public static string? SanitizeHumanFeedback(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var trimmed = text.Trim();
        return IsLikelyAiStructuredBlock(trimmed) ? null : trimmed;
    }

    public static string MapMessageRoleForApi(string? dbRole) =>
        dbRole?.Trim() switch
        {
            "User" => "student",
            "Assistant" => "assistant",
            "Lecturer" => "lecturer",
            "Expert" => "expert",
            _ => "system"
        };
}
