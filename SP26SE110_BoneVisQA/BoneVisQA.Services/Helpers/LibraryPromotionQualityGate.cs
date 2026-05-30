using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Services.Models.Expert;

namespace BoneVisQA.Services.Helpers;

/// <summary>Hard gate before expert promotion to the case library (ontology, diagnosis, ROI).</summary>
public static class LibraryPromotionQualityGate
{
    public static void ValidateOrThrow(
        PromoteToLibraryRequestDto request,
        VisualQASession session,
        IReadOnlyCollection<QAMessage> orderedMessages,
        ExpertReview? expertReviewForExpert)
    {
        MedicalOntologyValidation.RequireOntologyValue("Modality", request.Modality, MedicalOntologyValidation.Modalities);
        MedicalOntologyValidation.RequireOntologyValue("AnatomySite", request.AnatomySite, MedicalOntologyValidation.AnatomySites);
        MedicalOntologyValidation.RequireOntologyValue("Laterality", request.Laterality, MedicalOntologyValidation.Lateralities);
        MedicalOntologyValidation.RequireOntologyValue("ViewPosition", request.ViewPosition, MedicalOntologyValidation.ViewPositions);
        MedicalOntologyValidation.RequireOntologyValue("PathologyGroup", request.PathologyGroup, MedicalOntologyValidation.PathologyGroups);
        MedicalOntologyValidation.RequireOntologyValue("Difficulty", request.Difficulty, MedicalOntologyValidation.Difficulties);
        MedicalOntologyValidation.RequireOntologyValue("SourceType", request.SourceType, MedicalOntologyValidation.SourceTypes);
        MedicalOntologyValidation.RequireQualityScore(request.QualityScore);

        if (string.IsNullOrWhiteSpace(request.ClinicalEvidence))
            throw new InvalidOperationException("ClinicalEvidence is required for library promotion.");

        if (request.DifferentialDiagnoses == null || request.DifferentialDiagnoses.Count < 2)
            throw new InvalidOperationException("At least two differential diagnosis lines are required for library promotion.");

        var cleaned = request.DifferentialDiagnoses
            .Select(s => s?.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (cleaned.Count < 2)
            throw new InvalidOperationException("Differential diagnoses must contain at least two distinct non-empty entries.");

        if (!SessionHasValidRoi(session, orderedMessages, expertReviewForExpert, request))
            throw new InvalidOperationException(
                "Promotion rejected: the session must include a valid normalized ROI bounding box (coordinates) on a user message, expert-corrected ROI, or promote annotations.");
    }

    public static bool SessionHasValidRoi(
        VisualQASession session,
        IReadOnlyCollection<QAMessage> orderedMessages,
        ExpertReview? expertReviewForExpert,
        PromoteToLibraryRequestDto request)
    {
        foreach (var m in orderedMessages.Where(x => string.Equals(x.Role, "User", StringComparison.OrdinalIgnoreCase)))
        {
            if (BoundingBoxParser.TryParseFromJson(m.Coordinates) != null)
                return true;
        }

        if (expertReviewForExpert?.CorrectedRoi != null
            && TryParseExpertRoiDoubleArray(expertReviewForExpert.CorrectedRoi) != null)
            return true;

        foreach (var ann in request.TurnAnnotations ?? Enumerable.Empty<PromoteCaseAnnotationDto>())
        {
            if (ann?.Coordinates != null && JsonElementToRoiParses(ann.Coordinates.Value))
                return true;
        }

        foreach (var ann in request.ImageAnnotations ?? Enumerable.Empty<PromoteCaseAnnotationDto>())
        {
            if (ann?.Coordinates != null && JsonElementToRoiParses(ann.Coordinates.Value))
                return true;
        }

        return false;
    }

    private static BoundingBoxParser.NormalizedBoundingBox? TryParseExpertRoiDoubleArray(string json)
    {
        try
        {
            var arr = JsonSerializer.Deserialize<double[]>(json);
            if (arr is not { Length: >= 4 })
                return null;
            var x = arr[0];
            var y = arr[1];
            var w = arr[2];
            var h = arr[3];
            var fake = $"{{\"x\":{x},\"y\":{y},\"width\":{w},\"height\":{h}}}";
            return BoundingBoxParser.TryParseFromJson(fake);
        }
        catch
        {
            return null;
        }
    }

    private static bool JsonElementToRoiParses(JsonElement el)
    {
        try
        {
            var s = el.ValueKind == JsonValueKind.String ? el.GetString() : el.GetRawText();
            return !string.IsNullOrWhiteSpace(s) && BoundingBoxParser.TryParseFromJson(s) != null;
        }
        catch
        {
            return false;
        }
    }
}
