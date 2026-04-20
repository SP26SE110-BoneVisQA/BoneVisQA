using System;
using System.Collections.Generic;

namespace BoneVisQA.Services.Helpers;

/// <summary>
/// Maps case difficulty to <c>medical_cases.difficulty</c> CHECK: <c>Easy | Medium | Hard</c>.
/// </summary>
public static class MedicalCaseDifficultyNormalizer
{
    public static readonly IReadOnlyList<string> AllowedCanonicalValues =
        new[] { "Easy", "Medium", "Hard" };

    /// <summary>Null, empty, or unknown values become <c>Medium</c>.</summary>
    public static string Normalize(string? difficulty)
    {
        if (string.IsNullOrWhiteSpace(difficulty))
            return "Medium";

        var t = difficulty.Trim();
        if (t.Equals("Easy", StringComparison.OrdinalIgnoreCase))
            return "Easy";
        if (t.Equals("Medium", StringComparison.OrdinalIgnoreCase))
            return "Medium";
        if (t.Equals("Hard", StringComparison.OrdinalIgnoreCase))
            return "Hard";

        if (t.Equals("Intermediate", StringComparison.OrdinalIgnoreCase)
            || t.Equals("Normal", StringComparison.OrdinalIgnoreCase)
            || t.Equals("Moderate", StringComparison.OrdinalIgnoreCase))
            return "Medium";

        if (t.Equals("Beginner", StringComparison.OrdinalIgnoreCase))
            return "Easy";
        if (t.Equals("Advanced", StringComparison.OrdinalIgnoreCase))
            return "Hard";

        return "Medium";
    }
}
