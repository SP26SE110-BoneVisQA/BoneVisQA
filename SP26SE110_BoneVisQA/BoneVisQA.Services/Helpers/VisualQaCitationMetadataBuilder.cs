using BoneVisQA.Repositories.Models;
using BoneVisQA.Services.Models.VisualQA;

namespace BoneVisQA.Services.Helpers;

public static class VisualQaCitationMetadataBuilder
{
    public static CitationItemDto FromDocumentChunk(DocumentChunk? chunk)
    {
        var resolvedStartPage = ResolveStartPage(chunk?.StartPage, chunk?.ChunkOrder);
        var resolvedEndPage = ResolveEndPage(chunk?.EndPage, resolvedStartPage);

        return new CitationItemDto
        {
            ChunkId = chunk?.Id ?? Guid.Empty,
            MedicalCaseId = null,
            ReferenceUrl = BuildCitationUrl(chunk?.Doc?.FilePath, resolvedStartPage),
            Href = BuildCitationUrl(chunk?.Doc?.FilePath, resolvedStartPage),
            PageNumber = resolvedStartPage,
            StartPage = resolvedStartPage,
            EndPage = resolvedEndPage,
            SourceText = chunk?.Content,
            DisplayLabel = !string.IsNullOrWhiteSpace(chunk?.Doc?.Title) ? chunk.Doc.Title.Trim() : BuildSnippet(chunk?.Content),
            PageLabel = BuildPageLabel(resolvedStartPage, resolvedEndPage),
            Snippet = BuildSnippet(chunk?.Content),
            Kind = "doc"
        };
    }

    public static CitationItemDto FromMedicalCase(MedicalCase medicalCase, string? sourceText)
    {
        return new CitationItemDto
        {
            ChunkId = Guid.Empty,
            MedicalCaseId = medicalCase.Id,
            ReferenceUrl = null,
            Href = null,
            PageNumber = null,
            StartPage = null,
            EndPage = null,
            SourceText = sourceText,
            DisplayLabel = string.IsNullOrWhiteSpace(medicalCase.Title) ? "Medical case reference" : medicalCase.Title.Trim(),
            PageLabel = null,
            Snippet = BuildSnippet(sourceText),
            Kind = "case"
        };
    }

    public static IReadOnlyList<CitationItemDto> DeserializeMany(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<CitationItemDto>();

        try
        {
            var parsed = System.Text.Json.JsonSerializer.Deserialize<List<CitationItemDto>>(json);
            if (parsed == null || parsed.Count == 0)
                return Array.Empty<CitationItemDto>();

            foreach (var item in parsed)
            {
                item.Kind = NormalizeKind(item.Kind);
                item.Href ??= item.ReferenceUrl;
                item.Snippet ??= BuildSnippet(item.SourceText);
                if (string.IsNullOrWhiteSpace(item.PageLabel))
                    item.PageLabel = BuildPageLabel(item.StartPage, item.EndPage);
            }

            return parsed;
        }
        catch
        {
            return Array.Empty<CitationItemDto>();
        }
    }

    public static string SerializeMany(IReadOnlyCollection<CitationItemDto>? citations)
    {
        if (citations == null || citations.Count == 0)
            return "[]";

        var normalized = citations.Select(c =>
        {
            c.Kind = NormalizeKind(c.Kind);
            c.Href ??= c.ReferenceUrl;
            c.Snippet ??= BuildSnippet(c.SourceText);
            c.PageLabel ??= BuildPageLabel(c.StartPage, c.EndPage);
            return c;
        }).ToList();

        return System.Text.Json.JsonSerializer.Serialize(normalized);
    }

    public static string NormalizeKind(string? kind)
    {
        return string.Equals(kind, "case", StringComparison.OrdinalIgnoreCase) ? "case" : "doc";
    }

    public static string? BuildCitationUrl(string? filePath, int? startPage)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return null;
        return startPage.HasValue && startPage.Value > 0
            ? $"{filePath}#page={startPage.Value}"
            : filePath;
    }

    public static string? BuildPageLabel(int? startPage, int? endPage)
    {
        if (!startPage.HasValue || startPage.Value <= 0)
            return null;
        if (!endPage.HasValue || endPage.Value <= 0 || endPage.Value == startPage.Value)
            return $"Page {startPage.Value}";
        return $"Pages {startPage.Value}-{endPage.Value}";
    }

    public static string BuildSnippet(string? sourceText)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
            return "Reference excerpt";

        var cleaned = sourceText
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
        return cleaned.Length <= 120 ? cleaned : cleaned[..120].TrimEnd() + "...";
    }

    public static int? ResolveStartPage(int? startPage, int? chunkOrder)
    {
        if (startPage.HasValue && startPage.Value > 0)
            return startPage.Value;
        return chunkOrder.HasValue ? chunkOrder.Value + 1 : null;
    }

    public static int? ResolveEndPage(int? endPage, int? resolvedStartPage)
    {
        if (endPage.HasValue && endPage.Value > 0)
            return endPage.Value;
        return resolvedStartPage;
    }
}
