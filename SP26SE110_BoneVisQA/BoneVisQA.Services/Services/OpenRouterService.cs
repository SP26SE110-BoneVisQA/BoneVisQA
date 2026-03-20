using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.VisualQA;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BoneVisQA.Services.Services;

public class OpenRouterService : IOpenRouterService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenRouterService> _logger;

    public OpenRouterService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<OpenRouterService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<VisualQAResponseDto> GenerateDiagnosticAnswerAsync(
        string questionText,
        string? annotatedImageBase64,
        IReadOnlyList<DocumentChunk> contextChunks,
        string? coordinates,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["OpenRouter:ApiKey"]
                     ?? _configuration["OPENROUTER_API_KEY"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("OpenRouter API key missing (OpenRouter:ApiKey or OPENROUTER_API_KEY). Falling back safely.");
            return new VisualQAResponseDto
            {
                AnswerText = "OpenRouter API key is not configured (OpenRouter:ApiKey).",
                Citations = new List<CitationItemDto>()
            };
        }

        var model = _configuration["OpenRouter:Model"] ?? "openrouter/healer-alpha";
        var prompt = BuildPrompt(questionText, contextChunks, coordinates);

        var contentParts = new List<Dictionary<string, object>>
        {
            new() { ["type"] = "text", ["text"] = prompt }
        };
        if (!string.IsNullOrEmpty(annotatedImageBase64))
        {
            contentParts.Add(new Dictionary<string, object>
            {
                ["type"] = "image_url",
                ["image_url"] = new Dictionary<string, string>
                {
                    ["url"] = $"data:image/jpeg;base64,{annotatedImageBase64}"
                }
            });
        }

        var body = new Dictionary<string, object>
        {
            ["model"] = model,
            ["messages"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["role"] = "user",
                    ["content"] = contentParts
                }
            },
            ["temperature"] = 0.2
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.TryAddWithoutValidation("HTTP-Referer", "https://bonevisqa.app");
        request.Headers.TryAddWithoutValidation("X-Title", "BoneVisQA");
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenRouter request failed");
            return new VisualQAResponseDto { AnswerText = "Failed to reach OpenRouter.", Citations = new() };
        }

        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("OpenRouter error {Status}: {Body}", response.StatusCode, raw);
            return new VisualQAResponseDto { AnswerText = $"OpenRouter error: {response.StatusCode}", Citations = new() };
        }

        try
        {
            return ParseOpenRouterResponse(raw, contextChunks);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Parse OpenRouter response");
            return new VisualQAResponseDto
            {
                AnswerText = raw.Length > 4000 ? raw[..4000] : raw,
                Citations = new()
            };
        }
    }

    private static string BuildPrompt(
        string question,
        IReadOnlyList<DocumentChunk> chunks,
        string? coordinates)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an Expert Radiologist assisting medical students.");
        sb.AppendLine();
        sb.AppendLine("An image is provided. If the user provided coordinates, a RED bounding box has been drawn on the image.");
        sb.AppendLine("You MUST focus your visual analysis specifically inside this RED rectangle. Ignore irrelevant areas outside the box.");
        sb.AppendLine("Answer based on the image and reference context. Cite [1], [2] when using context.");
        sb.AppendLine();

        if (chunks.Count > 0)
        {
            sb.AppendLine("## Retrieved reference context:");
            sb.AppendLine();
            for (var i = 0; i < chunks.Count; i++)
            {
                sb.Append('[').Append(i + 1).Append("] ");
                sb.AppendLine(chunks[i].Content ?? string.Empty);
                sb.AppendLine();
            }
        }

        if (!string.IsNullOrWhiteSpace(coordinates))
        {
            sb.AppendLine($"User indicated region of interest (coordinates): {coordinates}");
        }

        sb.AppendLine();
        sb.AppendLine("## User question");
        sb.AppendLine(question);
        sb.AppendLine();
        sb.AppendLine("Bạn bắt buộc phải suy luận, giải thích và trả lời hoàn toàn bằng Tiếng Việt (Vietnamese) theo đúng chuẩn Y khoa.");
        sb.AppendLine();
        sb.AppendLine(@"Respond with JSON only:
{""answerText"":""..."",""suggestedDiagnosis"":""..."",""differentialDiagnoses"":""..."",""citationChunkIds"":[""uuid""]}
Use chunk id values from context. Empty array if none used.");

        return sb.ToString();
    }

    private static VisualQAResponseDto ParseOpenRouterResponse(string raw, IReadOnlyList<DocumentChunk> chunks)
    {
        using var doc = JsonDocument.Parse(raw);
        var choices = doc.RootElement.GetProperty("choices");
        if (choices.GetArrayLength() == 0)
            throw new InvalidOperationException("No choices");

        var content = choices[0].GetProperty("message").GetProperty("content");
        var text = content.ValueKind == JsonValueKind.String
            ? content.GetString() ?? ""
            : content.ToString();

        if (string.IsNullOrWhiteSpace(text))
            return new VisualQAResponseDto { AnswerText = "Empty model output.", Citations = new() };

        var jsonStart = text.IndexOf('{');
        var jsonEnd = text.LastIndexOf('}');
        if (jsonStart < 0 || jsonEnd < jsonStart)
            return new VisualQAResponseDto { AnswerText = text, Citations = new() };

        var slice = text[jsonStart..(jsonEnd + 1)];
        using var parsed = JsonDocument.Parse(slice);
        var root = parsed.RootElement;

        var answer = root.TryGetProperty("answerText", out var a) ? a.GetString() : null;
        var sugg = root.TryGetProperty("suggestedDiagnosis", out var s) ? s.GetString() : null;
        var diff = root.TryGetProperty("differentialDiagnoses", out var d) ? d.GetString() : null;

        var citedIds = new HashSet<Guid>();
        if (root.TryGetProperty("citationChunkIds", out var ids) && ids.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in ids.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.String && Guid.TryParse(el.GetString(), out var g))
                    citedIds.Add(g);
            }
        }

        var chunkById = chunks.ToDictionary(c => c.Id);
        var citations = new List<CitationItemDto>();
        foreach (var id in citedIds)
        {
            if (chunkById.TryGetValue(id, out var ch))
            {
                citations.Add(new CitationItemDto
                {
                    ChunkId = ch.Id,
                    SimilarityScore = 0,
                    SourceText = ch.Content?.Length > 500 ? ch.Content[..500] : ch.Content
                });
            }
        }

        if (citations.Count == 0 && chunks.Count > 0)
        {
            foreach (var ch in chunks.Take(3))
                citations.Add(new CitationItemDto { ChunkId = ch.Id, SourceText = ch.Content });
        }

        return new VisualQAResponseDto
        {
            AnswerText = answer ?? text,
            SuggestedDiagnosis = sugg,
            DifferentialDiagnoses = diff,
            Citations = citations
        };
    }
}
