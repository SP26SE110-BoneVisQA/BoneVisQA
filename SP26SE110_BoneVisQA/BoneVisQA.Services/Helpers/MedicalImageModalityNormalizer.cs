using System;
using System.Collections.Generic;

namespace BoneVisQA.Services.Helpers;

/// <summary>
/// Maps free-text modality to values allowed by
/// <c>CHECK (modality = ANY (ARRAY['X-Ray','CT','MRI','Ultrasound','Other']))</c> on <c>medical_images</c>.
/// </summary>
public static class MedicalImageModalityNormalizer
{
    /// <summary>Exact strings stored in PostgreSQL (must match DB check constraint).</summary>
    public static readonly IReadOnlyList<string> AllowedCanonicalValues =
        new[] { "X-Ray", "CT", "MRI", "Ultrasound", "Other" };

    /// <summary>Returns a modality that satisfies <c>medical_images_modality_check</c>.</summary>
    public static string Normalize(string? modality)
    {
        if (string.IsNullOrWhiteSpace(modality))
            return "Other";

        var trimmed = modality.Trim();

        if (trimmed.Equals("X-Ray", StringComparison.OrdinalIgnoreCase))
            return "X-Ray";
        if (trimmed.Equals("CT", StringComparison.OrdinalIgnoreCase))
            return "CT";
        if (trimmed.Equals("MRI", StringComparison.OrdinalIgnoreCase))
            return "MRI";
        if (trimmed.Equals("Ultrasound", StringComparison.OrdinalIgnoreCase))
            return "Ultrasound";
        if (trimmed.Equals("Other", StringComparison.OrdinalIgnoreCase))
            return "Other";

        var compact = trimmed
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        if (compact.Equals("XRay", StringComparison.OrdinalIgnoreCase)
            || compact.Equals("XR", StringComparison.OrdinalIgnoreCase)
            || compact.Equals("CR", StringComparison.OrdinalIgnoreCase)
            || compact.Equals("DR", StringComparison.OrdinalIgnoreCase)
            || compact.Equals("DX", StringComparison.OrdinalIgnoreCase)
            || compact.Equals("Radiograph", StringComparison.OrdinalIgnoreCase)
            || compact.Equals("Radiography", StringComparison.OrdinalIgnoreCase)
            || compact.Equals("Plainfilm", StringComparison.OrdinalIgnoreCase))
            return "X-Ray";

        if (compact.Equals("MR", StringComparison.OrdinalIgnoreCase)
            || compact.Equals("MRI", StringComparison.OrdinalIgnoreCase))
            return "MRI";

        if (compact.Equals("CT", StringComparison.OrdinalIgnoreCase))
            return "CT";

        if (compact.Equals("US", StringComparison.OrdinalIgnoreCase)
            || compact.Equals("Ultrasound", StringComparison.OrdinalIgnoreCase))
            return "Ultrasound";

        return "Other";
    }
}
