using System;
using System.Linq;

namespace BoneVisQA.Services.Helpers;

/// <summary>Validates document-level defaults for knowledge-base PDF uploads.</summary>
public static class DocumentMetadataValidation
{
    public const string DefaultModality = "X-Ray";

    /// <summary>Maps DICOM codes (e.g. DX) to canonical ontology literals; defaults to <see cref="DefaultModality"/>.</summary>
    public static string ResolveModality(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return DefaultModality;

        var v = value.Trim();
        if (DicomOntologyMappingHelper.TryMapModality(v, out var mapped))
            return mapped;

        if (MedicalOntologyValidation.Modalities.Contains(v))
            return v;

        throw new InvalidOperationException(
            $"Modality must be one of [{string.Join(", ", MedicalOntologyValidation.Modalities.OrderBy(x => x))}] or a supported DICOM code (e.g. DX).");
    }

    public static string RequireModality(string? value) => ResolveModality(value);

    public static string? NormalizeOptionalPathology(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var v = value.Trim();
        if (!MedicalOntologyValidation.PathologyGroups.Contains(v))
        {
            throw new InvalidOperationException(
                $"Pathology group must be one of [{string.Join(", ", MedicalOntologyValidation.PathologyGroups.OrderBy(x => x))}] or empty.");
        }

        return v;
    }
}
