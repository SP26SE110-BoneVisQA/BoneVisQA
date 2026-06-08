using System.Text.Json;
using System.Text.Json.Serialization;
using BoneVisQA.Services.Models.Expert;
using Microsoft.AspNetCore.Http;

namespace BoneVisQA.Services.Helpers;

/// <summary>Reads <see cref="PromoteToLibraryRequestDto"/> without relying on <c>[FromBody]</c> (avoids 415 when Content-Type is missing).</summary>
public static class PromoteToLibraryRequestReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new NullableGuidLenientJsonConverter() },
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public static async Task<PromoteToLibraryRequestDto?> ReadAsync(
        HttpRequest request,
        CancellationToken cancellationToken = default)
    {
        return await ReadCoreAsync<PromoteToLibraryRequestDto>(request, cancellationToken);
    }

    public static async Task<ApproveAndPromoteToLibraryRequestDto?> ReadApproveAndPromoteAsync(
        HttpRequest request,
        CancellationToken cancellationToken = default)
    {
        return await ReadCoreAsync<ApproveAndPromoteToLibraryRequestDto>(request, cancellationToken);
    }

    private static async Task<T?> ReadCoreAsync<T>(
        HttpRequest request,
        CancellationToken cancellationToken = default)
        where T : PromoteToLibraryRequestDto, new()
    {
        if (request.ContentLength is 0 or null)
            return null;

        if (request.HasFormContentType)
            return ReadFromForm(request) as T;

        request.EnableBuffering();
        request.Body.Position = 0;
        using var reader = new StreamReader(request.Body, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        request.Body.Position = 0;

        if (string.IsNullOrWhiteSpace(text))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(text, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static PromoteToLibraryRequestDto ReadFromForm(HttpRequest request)
    {
        var form = request.Form;
        var dto = new PromoteToLibraryRequestDto
        {
            Title = form["title"].FirstOrDefault(),
            CategoryName = form["categoryName"].FirstOrDefault(),
            Difficulty = form["difficulty"].FirstOrDefault() ?? string.Empty,
            KeyFindings = form["keyFindings"].FirstOrDefault() ?? string.Empty,
            ReflectiveQuestions = form["reflectiveQuestions"].FirstOrDefault() ?? string.Empty,
            SuggestedDiagnosis = form["suggestedDiagnosis"].FirstOrDefault() ?? string.Empty,
            Description = form["description"].FirstOrDefault() ?? string.Empty,
            Modality = form["modality"].FirstOrDefault() ?? string.Empty,
            AnatomySite = form["anatomySite"].FirstOrDefault() ?? string.Empty,
            Laterality = form["laterality"].FirstOrDefault() ?? string.Empty,
            ViewPosition = form["viewPosition"].FirstOrDefault() ?? string.Empty,
            PathologyGroup = form["pathologyGroup"].FirstOrDefault() ?? string.Empty,
            SourceType = form["sourceType"].FirstOrDefault() ?? string.Empty,
            ClinicalEvidence = form["clinicalEvidence"].FirstOrDefault() ?? string.Empty,
        };

        if (Guid.TryParse(form["categoryId"].FirstOrDefault(), out var categoryId))
            dto.CategoryId = categoryId;

        if (float.TryParse(form["qualityScore"].FirstOrDefault(), out var quality))
            dto.QualityScore = quality;

        var diffs = form["differentialDiagnoses"].Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        if (diffs.Count > 0)
            dto.DifferentialDiagnoses = diffs;

        return dto;
    }
}
