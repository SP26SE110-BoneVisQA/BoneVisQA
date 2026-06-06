using System;
using System.Collections.Generic;
using System.Linq;

namespace BoneVisQA.Services.Helpers;

/// <summary>
/// DICOM tag → canonical ontology literals for FE auto-fill (aligned with Python ingest).
/// </summary>
public static class DicomOntologyMappingHelper
{
    private static readonly IReadOnlyDictionary<string, string> ModalityMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CT"] = "CT",
            ["CTA"] = "CT",
            ["MR"] = "MRI",
            ["MRI"] = "MRI",
            ["US"] = "Ultrasound",
            ["USD"] = "Ultrasound",
            ["DX"] = "X-Ray",
            ["CR"] = "X-Ray",
            ["XR"] = "X-Ray",
            ["XA"] = "X-Ray",
            ["RF"] = "X-Ray",
            ["MG"] = "X-Ray",
            ["PT"] = "X-Ray",
            ["NM"] = "X-Ray",
            ["OT"] = "X-Ray",
        };

    private static readonly IReadOnlyDictionary<string, string> BodyPartMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PELVIS"] = "Pelvis",
            ["HIP"] = "Hip",
            ["LOW_EXM"] = "Other",
            ["LOWER_EXTREMITY"] = "Other",
            ["LOWER EXTREMITY"] = "Other",
            ["UP_EXM"] = "Other",
            ["UPPER_EXTREMITY"] = "Other",
            ["UPPER EXTREMITY"] = "Other",
            ["FOOT"] = "Foot",
            ["ANKLE"] = "Ankle",
            ["KNEE"] = "Knee",
            ["FEMUR"] = "Femur",
            ["TIBIA"] = "Tibia",
            ["FIBULA"] = "Fibula",
            ["HAND"] = "Hand",
            ["WRIST"] = "Wrist",
            ["ELBOW"] = "Elbow",
            ["SHOULDER"] = "Shoulder",
            ["HUMERUS"] = "Shoulder",
            ["CLAVICLE"] = "Shoulder",
            ["RADIUS"] = "Hand",
            ["ULNA"] = "Hand",
            ["CSPINE"] = "Spine",
            ["TSPINE"] = "Spine",
            ["LSPINE"] = "Spine",
            ["SPINE"] = "Spine",
            ["LUMBAR"] = "Spine",
            ["CERVICAL"] = "Spine",
        };

    public static string? MapDicomModality(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var key = raw.Trim();
        if (ModalityMap.TryGetValue(key, out var mapped))
            return mapped;

        return "X-Ray";
    }

    public static string? MapDicomBodyPart(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var normalized = raw.Trim().ToUpperInvariant().Replace(' ', '_');
        if (BodyPartMap.TryGetValue(normalized, out var exact))
            return exact;

        foreach (var (token, site) in BodyPartMap)
        {
            if (normalized.Contains(token, StringComparison.OrdinalIgnoreCase)
                || token.Contains(normalized, StringComparison.OrdinalIgnoreCase))
            {
                return site;
            }
        }

        return "Other";
    }

    public static bool TryMapModality(string? dicomCode, out string canonicalModality)
    {
        canonicalModality = string.Empty;
        if (string.IsNullOrWhiteSpace(dicomCode))
            return false;

        return ModalityMap.TryGetValue(dicomCode.Trim(), out canonicalModality!);
    }

    public static IReadOnlyDictionary<string, string> GetDicomModalityMap() => ModalityMap;

    public static IReadOnlyDictionary<string, string> GetDicomBodyPartMap() => BodyPartMap;

    public static IReadOnlyList<string> GetModalities() =>
        MedicalOntologyValidation.Modalities.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();

    public static IReadOnlyList<string> GetAnatomySites() =>
        MedicalOntologyValidation.AnatomySites.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();

    public static IReadOnlyList<string> GetPathologyGroups() =>
        MedicalOntologyValidation.PathologyGroups.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
}
