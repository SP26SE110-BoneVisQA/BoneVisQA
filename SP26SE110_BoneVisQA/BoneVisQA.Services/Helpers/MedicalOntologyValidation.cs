using System;
using System.Collections.Generic;
using System.Linq;

namespace BoneVisQA.Services.Helpers;

/// <summary>Strict ontology literals aligned with Supabase <c>case_metadata</c> and Python ingest.</summary>
public static class MedicalOntologyValidation
{
    public static readonly IReadOnlySet<string> Modalities =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "X-Ray", "CT", "MRI", "Ultrasound" };

    public static readonly IReadOnlySet<string> AnatomySites =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Spine", "Hip", "Knee", "Wrist", "Shoulder", "Ankle", "Pelvis", "Foot", "Hand", "Elbow",
            "Femur", "Tibia", "Fibula", "Other"
        };

    public static readonly IReadOnlySet<string> Lateralities =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Left", "Right", "Bilateral", "Not-Applicable" };

    public static readonly IReadOnlySet<string> ViewPositions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "AP", "Lateral", "Oblique", "PA" };

    public static readonly IReadOnlySet<string> PathologyGroups =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Trauma", "Degenerative", "Infection", "Tumor", "Congenital"
        };

    public static readonly IReadOnlySet<string> Difficulties =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Easy", "Medium", "Hard" };

    public static readonly IReadOnlySet<string> SourceTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Clinical", "Training", "Research" };

    public static string RequireOntologyValue(string label, string? value, IReadOnlySet<string> allowed)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{label} is required for library promotion.");
        var v = value.Trim();
        if (!allowed.Contains(v))
            throw new InvalidOperationException($"{label} must be one of [{string.Join(", ", allowed.OrderBy(x => x))}].");
        return v;
    }

    public static void RequireQualityScore(float score)
    {
        if (float.IsNaN(score) || score < 0f || score > 1f)
            throw new InvalidOperationException("QualityScore must be between 0.0 and 1.0.");
    }
}
