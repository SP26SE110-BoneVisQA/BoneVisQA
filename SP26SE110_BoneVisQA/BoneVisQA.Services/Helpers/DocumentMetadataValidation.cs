using System;
using System.Linq;

namespace BoneVisQA.Services.Helpers;

/// <summary>Validates document-level defaults for knowledge-base PDF uploads.</summary>
public static class DocumentMetadataValidation
{
    public static string RequireModality(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Modality is required when uploading a document.");

        var v = value.Trim();
        if (!MedicalOntologyValidation.Modalities.Contains(v))
        {
            throw new InvalidOperationException(
                $"Modality must be one of [{string.Join(", ", MedicalOntologyValidation.Modalities.OrderBy(x => x))}].");
        }

        return v;
    }

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
