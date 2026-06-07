using BoneVisQA.Services.Models.Expert;

namespace BoneVisQA.Services.Helpers;

/// <summary>Validates promote-to-library payloads after server-side hydration.</summary>
public static class PromoteToLibraryValidation
{
    public static Dictionary<string, string[]>? ValidateRequiredFields(PromoteToLibraryRequestDto request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(request.Title) &&
            string.IsNullOrWhiteSpace(request.Description) &&
            string.IsNullOrWhiteSpace(request.SuggestedDiagnosis))
        {
            // Title alone is optional when hydrator fills it; clinical fields matter most.
        }

        if (string.IsNullOrWhiteSpace(request.Description))
            errors["description"] = ["Description is required."];

        if (string.IsNullOrWhiteSpace(request.SuggestedDiagnosis))
            errors["suggestedDiagnosis"] = ["SuggestedDiagnosis is required."];

        if (string.IsNullOrWhiteSpace(request.KeyFindings))
            errors["keyFindings"] = ["KeyFindings is required."];

        if (string.IsNullOrWhiteSpace(request.ReflectiveQuestions))
            errors["reflectiveQuestions"] = ["ReflectiveQuestions is required."];

        if ((request.CategoryId is null || request.CategoryId == Guid.Empty) &&
            string.IsNullOrWhiteSpace(request.CategoryName))
        {
            errors["categoryId"] = ["CategoryId or categoryName is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Difficulty))
            errors["difficulty"] = ["Difficulty is required."];

        if (request.TagNames == null || request.TagNames.Count == 0 ||
            request.TagNames.All(t => string.IsNullOrWhiteSpace(t)))
        {
            errors["tagNames"] = ["At least one tag is required."];
        }

        return errors.Count == 0 ? null : errors;
    }
}
