using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BoneVisQA.Domain.Settings;
using BoneVisQA.Services.Exceptions;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.VisualQA;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BoneVisQA.Services.Services;

public class GeminiService : IGeminiService
{
    public const string HttpClientName = "Gemini";
    private const string InvalidImageNotXrayGuardrail = "INVALID_IMAGE_NOT_XRAY";
    private const string MimeTypeJpeg = "image/jpeg";
    private const string FallbackNoReliableInfoAnswer =
        "Sorry, based on our musculoskeletal medical knowledge base, I could not find sufficiently reliable information to answer this advanced question.";
    private const string NoContextAnswer =
        "The current medical data does not contain enough information to answer this question.";

    /// <param name="ragContextAdequate">False when vector retrieval found no/weak chunks — model may use general knowledge and must append the disclaimer (see implementation).</param>
    private static string BuildSystemPrompt(bool ragContextAdequate)
    {
        var ragPolicy = ragContextAdequate
            ? "Only when the image (if any) is valid medical data, the question is related to musculoskeletal medicine, and context is sufficient should you fill diagnosis, differential_diagnoses, findings, reflective_questions, and citations.\n" +
              "Always prioritize RAG context. If context is insufficient, set diagnosis exactly to: '" + NoContextAnswer + "' and set differential_diagnoses, findings, reflective_questions, and citations ([]) to null or empty.\n"
            : "The document library (RAG context) may be weak or empty. Still answer in professional medical Vietnamese using general musculoskeletal knowledge and the image when present.\n" +
              "If there are NO clear radiographic signs (e.g. no definite fracture in the ROI), say so in plain short Vietnamese in the diagnosis field only — do NOT paste long template disclaimers, do NOT invent citations, and leave citations as [] unless RAG chunks were actually used.\n" +
              "Do not fill findings/differential/citations with boilerplate or restate the prompt; use null or empty arrays when there is nothing meaningful to add.\n";

        return
            "STEP 1: Analyze if the provided image is a Human Bone X-Ray. If it is NOT (e.g., it is a CT scan, MRI, an animal, or a random object), YOU MUST refuse to answer medical questions and output EXACTLY this string: 'INVALID_IMAGE_NOT_XRAY'. Stop processing further.\n" +
            "\n" +
            "MANDATORY QUESTION AND IMAGE ANALYSIS (STRICT REFUSAL RULES):\n" +
            "If the question is NOT related to musculoskeletal medicine, health, or musculoskeletal imaging (for example: fuel prices, weather, politics, programming), you MUST answer exactly with: 'Your question is not related to the musculoskeletal medical domain. Please provide a valid professional medical question.'\n" +
            "Do not answer with 'The database has no information'.\n" +
            "When refusing under this rule: set diagnosis, differential_diagnoses, findings, and reflective_questions to null, citations to [] or null, and IGNORE ALL OTHER REQUESTS.\n" +
            "\n" +
            "MANDATORY IMAGE VALIDATION (IF PROVIDED):\n" +
            "1. If the provided image is NOT a musculoskeletal medical image (for example: landscapes, animals, ordinary people, objects, or non-medical images), YOU MUST REFUSE by setting `diagnosis` to 'The provided image is not valid medical data.' and setting differential_diagnoses, findings, and reflective_questions to null and citations to []. Ignore all other requests.\n" +
            "\n" +
            "You MUST output a JSON object with EXACTLY these keys: 'diagnosis', 'findings', 'differential_diagnoses', 'reflective_questions', 'citations' (array of objects { \"kind\": \"Doc\"|\"Case\", \"id\": \"uuid\" }).\n" +
            "If the question is explicitly binary and the evidence is decisive, diagnosis may begin with a concise yes/no conclusion. If the evidence is incomplete, state uncertainty instead of forcing a yes/no answer.\n" +
            "Never flip left/right laterality unless the image, ROI, retrieved references, or prior conversation explicitly supports the change. If laterality is uncertain, say so instead of guessing.\n" +
            "In diagnosis/findings, when citing the library, use [Doc:UUID] for document chunks and [Case:UUID] for medical cases, matching the citations array.\n" +
            "\n" +
            "Start the answer immediately. NO greetings. NO introduction.\n" +
            "DO NOT output content outside the listed JSON fields.\n" +
            "You must return a raw JSON object without any markdown wrapping like ```json.\n" +
            "When answering valid domain questions, respond in accurate professional medical Vietnamese.\n" +
            "\n" +
            ragPolicy +
            "\n" +
            "RETURN EXACTLY 1 JSON OBJECT and do not append any other content.\n" +
            "Do not embed citationChunkIds or extra raw JSON inside diagnosis/findings.\n" +
            "DO NOT create or estimate confidence/aiConfidenceScore fields in JSON—the system assigns scores using RAG math.";
    }

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GeminiSettings _settings;
    private readonly ILogger<GeminiService> _logger;

    public GeminiService(
        IOptions<GeminiSettings> options,
        IHttpClientFactory httpClientFactory,
        ILogger<GeminiService> logger)
    {
        _settings = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public VisualQAResponseDto? TryGetUnavailableFallbackResponse()
    {
        var apiKeys = _settings.GetResolvedApiKeys();
        if (apiKeys.Count == 0)
        {
            _logger.LogWarning("Gemini API keys missing (Gemini:ApiKeys). Returning empty safe response.");
            return BuildFallbackClarificationDto(FallbackNoReliableInfoAnswer);
        }

        var modelIds = _settings.GetResolvedModelIds();
        if (modelIds.Count == 0)
        {
            _logger.LogWarning(
                "Gemini model ids missing (configure Gemini:Models or Gemini:ModelId). Returning empty safe response.");
            return BuildFallbackClarificationDto(FallbackNoReliableInfoAnswer);
        }

        if (string.IsNullOrWhiteSpace(_settings.BaseUrl))
        {
            _logger.LogWarning("Gemini base URL missing (Gemini:BaseUrl). Returning empty safe response.");
            return BuildFallbackClarificationDto(FallbackNoReliableInfoAnswer);
        }

        return null;
    }

    public VisualQAResponseDto ParseMedicalAnswerFromRawResponse(string rawJson) =>
        ParseGeminiResponse(rawJson);

    private static VisualQAResponseDto BuildFallbackClarificationDto(string message) =>
        new()
        {
            AnswerText = message,
            SuggestedDiagnosis = null,
            DifferentialDiagnoses = null,
            KeyImagingFindings = null,
            ReflectiveQuestions = null,
            AiConfidenceScore = null,
            ErrorMessage = message,
            ResponseKind = "clarification",
            Citations = new List<CitationItemDto>()
        };

    public async Task<VisualQAResponseDto> GenerateMedicalAnswerAsync(
        string prompt,
        string imageUrl,
        string? conversationHistory = null,
        bool ragContextAdequate = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt must not be empty.", nameof(prompt));

        var unavailable = TryGetUnavailableFallbackResponse();
        if (unavailable != null)
            return unavailable;

        var apiKeys = _settings.GetResolvedApiKeys();
        var modelIds = _settings.GetResolvedModelIds();
        var baseUrl = _settings.BaseUrl!.TrimEnd('/');

        string? base64Image = null;
        string? mimeType = null;

        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            (base64Image, mimeType) = await ResolveImageToBase64Async(imageUrl, cancellationToken);
            if (base64Image == null)
            {
                return new VisualQAResponseDto
                {
                    AnswerText = "Unable to access medical images from storage.",
                    SuggestedDiagnosis = null,
                    DifferentialDiagnoses = null,
                    KeyImagingFindings = null,
                    ReflectiveQuestions = null,
                    AiConfidenceScore = null,
                    ErrorMessage = "Unable to access medical images from storage.",
                    ResponseKind = "clarification",
                    Citations = new List<CitationItemDto>()
                };
            }
        }

        var failureSummaries = new List<string>();
        var totalAttempts = apiKeys.Count * modelIds.Count;
        var attemptIndex = 0;

        for (var keyIndex = 0; keyIndex < apiKeys.Count; keyIndex++)
        {
            var apiKey = apiKeys[keyIndex];
            var shouldAdvanceKey = false;
            foreach (var modelId in modelIds)
            {
                attemptIndex++;
                _logger.LogInformation(
                    "[GeminiService] generateContent attempt {AttemptIndex}/{TotalAttempts} using key #{KeyIndex} and model id \"{ModelId}\"",
                    attemptIndex,
                    totalAttempts,
                    keyIndex + 1,
                    modelId);
                var endpoint = $"{baseUrl}/models/{modelId}:generateContent?key={apiKey}";

                try
                {
                    var payload = BuildVisionPayload(prompt, conversationHistory, base64Image, mimeType ?? MimeTypeJpeg, ragContextAdequate);

                    using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
                    req.Content = new StringContent(
                        JsonSerializer.Serialize(payload),
                        Encoding.UTF8,
                        "application/json");

                    var client = _httpClientFactory.CreateClient(HttpClientName);
                    _logger.LogDebug("[GeminiService] POST {BaseUrl}/models/{ModelId}:generateContent", baseUrl, modelId);
                    var resp = await client.SendAsync(req, cancellationToken);
                    var bytes = await resp.Content.ReadAsByteArrayAsync(cancellationToken);
                    var raw = Encoding.UTF8.GetString(bytes);

                    _logger.LogInformation(
                        "[GeminiService] Model id \"{ModelId}\": HTTP {StatusCode}; raw response (first 2000 chars): {Raw}",
                        modelId,
                        (int)resp.StatusCode,
                        raw.Length > 2000 ? raw[..2000] : raw);

                    if (!resp.IsSuccessStatusCode)
                    {
                        var bodySnippet = raw.Length > 500 ? raw[..500] : raw;
                        failureSummaries.Add($"{modelId}: HTTP {(int)resp.StatusCode} — {bodySnippet}");

                        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            _logger.LogWarning(
                                "[GeminiService] Model id \"{ModelId}\" returned HTTP 404 (not available for this key/project). No retry on this id — advancing to next configured model.",
                                modelId);
                            continue;
                        }

                        if (resp.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                        {
                            shouldAdvanceKey = true;
                            _logger.LogWarning(
                                "[GeminiService] Key #{KeyIndex} rejected with HTTP {StatusCode}. Advancing to next API key.",
                                keyIndex + 1,
                                (int)resp.StatusCode);
                            break;
                        }

                        if (resp.StatusCode == (System.Net.HttpStatusCode)429)
                        {
                            shouldAdvanceKey = true;
                            _logger.LogWarning(
                                "[GeminiService] Key #{KeyIndex} hit quota/rate limit on model \"{ModelId}\". Advancing to next API key.",
                                keyIndex + 1,
                                modelId);
                            break;
                        }

                        if (IsTransient(resp.StatusCode))
                        {
                            _logger.LogWarning(
                                "[GeminiService] Model id \"{ModelId}\" reached a transient error (HTTP {StatusCode}). Trying next model on the same key.",
                                modelId,
                                (int)resp.StatusCode);
                            await Task.Delay(1000, cancellationToken);
                            continue;
                        }

                        _logger.LogWarning(
                            "[GeminiService] Model id \"{ModelId}\": HTTP {StatusCode}. Trying next configured model.",
                            modelId,
                            (int)resp.StatusCode);
                        continue;
                    }

                    _logger.LogInformation(
                        "[GeminiService] Model id \"{ModelId}\" returned success (HTTP {StatusCode}); parsing response body.",
                        modelId,
                        (int)resp.StatusCode);

                    try
                    {
                        var parsed = ParseGeminiResponse(raw);
                        _logger.LogInformation(
                            "[GeminiService] Completed successfully using key #{KeyIndex} and model id \"{ModelId}\" (attempt {AttemptIndex}/{TotalAttempts}).",
                            keyIndex + 1,
                            modelId,
                            attemptIndex,
                            totalAttempts);
                        return parsed;
                    }
                    catch (AiResponseFormatException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        failureSummaries.Add($"{modelId}: response parse error — {ex.Message}");
                        _logger.LogWarning(
                            ex,
                            "[GeminiService] Failed to parse Gemini JSON for model id \"{ModelId}\". Trying next configured model.",
                            modelId);
                        continue;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failureSummaries.Add($"{modelId}: {ex.GetType().Name} — {ex.Message}");
                    _logger.LogWarning(
                        ex,
                        "[GeminiService] Model id \"{ModelId}\" request failed. Trying next model or key...",
                        modelId);
                    await Task.Delay(1000, cancellationToken);
                    continue;
                }
            }

            if (shouldAdvanceKey)
                continue;
        }

        throw new InvalidOperationException(
            $"All configured Gemini keys/models failed ({apiKeys.Count} key(s), {modelIds.Count} model(s)). Summary: {string.Join(" | ", failureSummaries)}");
    }

    public async IAsyncEnumerable<string> StreamMedicalAnswerRawAsync(
        string prompt,
        string imageUrl,
        string? conversationHistory = null,
        bool ragContextAdequate = true,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt must not be empty.", nameof(prompt));

        var unavailable = TryGetUnavailableFallbackResponse();
        if (unavailable != null)
            yield break;

        var apiKeys = _settings.GetResolvedApiKeys();
        var modelIds = _settings.GetResolvedModelIds();
        var baseUrl = _settings.BaseUrl!.TrimEnd('/');

        string? base64Image = null;
        string? mimeType = null;

        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            (base64Image, mimeType) = await ResolveImageToBase64Async(imageUrl, cancellationToken).ConfigureAwait(false);
            if (base64Image == null)
                yield break;
        }

        var failureSummaries = new List<string>();
        var totalAttempts = apiKeys.Count * modelIds.Count;
        var attemptIndex = 0;

        for (var keyIndex = 0; keyIndex < apiKeys.Count; keyIndex++)
        {
            var apiKey = apiKeys[keyIndex];
            var shouldAdvanceKey = false;
            foreach (var modelId in modelIds)
            {
                attemptIndex++;
                var endpoint =
                    $"{baseUrl}/models/{modelId}:streamGenerateContent?key={Uri.EscapeDataString(apiKey)}&alt=sse";

                _logger.LogInformation(
                    "[GeminiService] streamGenerateContent attempt {AttemptIndex}/{TotalAttempts} using key #{KeyIndex} and model id \"{ModelId}\"",
                    attemptIndex,
                    totalAttempts,
                    keyIndex + 1,
                    modelId);

                var payload = BuildVisionPayload(prompt, conversationHistory, base64Image, mimeType ?? MimeTypeJpeg, ragContextAdequate);

                using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
                req.Content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json");

                var client = _httpClientFactory.CreateClient(HttpClientName);
                _logger.LogDebug("[GeminiService] POST {BaseUrl}/models/{ModelId}:streamGenerateContent?alt=sse", baseUrl, modelId);

                HttpResponseMessage resp;
                try
                {
                    resp = await client
                        .SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failureSummaries.Add($"{modelId}: {ex.GetType().Name} — {ex.Message}");
                    _logger.LogWarning(
                        ex,
                        "[GeminiService] Model id \"{ModelId}\" stream request failed. Trying next model or key...",
                        modelId);
                    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                using (resp)
                {
                    if (!resp.IsSuccessStatusCode)
                    {
                        var raw = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                        var bodySnippet = raw.Length > 500 ? raw[..500] : raw;
                        failureSummaries.Add($"{modelId}: HTTP {(int)resp.StatusCode} — {bodySnippet}");

                        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            _logger.LogWarning(
                                "[GeminiService] Model id \"{ModelId}\" stream returned HTTP 404. Advancing to next model.",
                                modelId);
                            continue;
                        }

                        if (resp.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                        {
                            shouldAdvanceKey = true;
                            _logger.LogWarning(
                                "[GeminiService] Key #{KeyIndex} rejected on stream with HTTP {StatusCode}. Advancing to next API key.",
                                keyIndex + 1,
                                (int)resp.StatusCode);
                            break;
                        }

                        if (resp.StatusCode == (System.Net.HttpStatusCode)429)
                        {
                            shouldAdvanceKey = true;
                            _logger.LogWarning(
                                "[GeminiService] Key #{KeyIndex} rate limited on stream for model \"{ModelId}\". Advancing to next API key.",
                                keyIndex + 1,
                                modelId);
                            break;
                        }

                        if (IsTransient(resp.StatusCode))
                        {
                            _logger.LogWarning(
                                "[GeminiService] Stream transient HTTP {StatusCode} for model \"{ModelId}\". Retrying next model.",
                                (int)resp.StatusCode,
                                modelId);
                            await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                            continue;
                        }

                        _logger.LogWarning(
                            "[GeminiService] Stream HTTP {StatusCode} for model \"{ModelId}\". Next model.",
                            (int)resp.StatusCode,
                            modelId);
                        continue;
                    }

                    await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                    await foreach (var fragment in ReadGeminiSseTextFragmentsAsync(stream, cancellationToken))
                        yield return fragment;
                }

                _logger.LogInformation(
                    "[GeminiService] Stream completed using key #{KeyIndex} and model id \"{ModelId}\" (attempt {AttemptIndex}/{TotalAttempts}).",
                    keyIndex + 1,
                    modelId,
                    attemptIndex,
                    totalAttempts);
                yield break;
            }

            if (shouldAdvanceKey)
                continue;
        }

        throw new InvalidOperationException(
            $"All configured Gemini keys/models failed ({apiKeys.Count} key(s), {modelIds.Count} model(s)). Summary: {string.Join(" | ", failureSummaries)}");
    }

    private async IAsyncEnumerable<string> ReadGeminiSseTextFragmentsAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream);
        string? line;
        var cumulativeCandidate = "";
        while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
                continue;
            if (trimmed.StartsWith(':'))
                continue;

            var payload = trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                ? trimmed.Substring(5).Trim()
                : trimmed;
            if (payload is "[DONE]" or "\"[DONE]\"")
                continue;

            string fragment;
            try
            {
                using var doc = JsonDocument.Parse(payload);
                fragment = ExtractStreamingTextFragment(doc.RootElement, ref cumulativeCandidate);
            }
            catch (JsonException)
            {
                continue;
            }

            if (fragment.Length > 0)
                yield return fragment;
        }
    }

    /// <summary>Handles both cumulative and incremental <c>candidates[0].content.parts[0].text</c> chunks.</summary>
    private static string ExtractStreamingTextFragment(JsonElement root, ref string cumulativeCandidateText)
    {
        var piece = ExtractCandidateText(root);
        if (piece.Length == 0)
            return string.Empty;

        if (cumulativeCandidateText.Length > 0 &&
            piece.Length >= cumulativeCandidateText.Length &&
            piece.StartsWith(cumulativeCandidateText, StringComparison.Ordinal))
        {
            var delta = piece[cumulativeCandidateText.Length..];
            cumulativeCandidateText = piece;
            return delta;
        }

        cumulativeCandidateText += piece;
        return piece;
    }

    private static Dictionary<string, object> BuildVisionPayload(
        string prompt,
        string? conversationHistory,
        string? base64Image,
        string mimeType,
        bool ragContextAdequate)
    {
        // Required Gemini multimodal structure for v1:
        // { "contents": [ { "parts": [ { "text": "..." }, { "inlineData": { "mimeType": "image/jpeg", "data": "..." } } ] } ] }
        // For prompt persona enforcement in v1, we inline the system prompt into the text portion.
        var systemPrompt = BuildSystemPrompt(ragContextAdequate);
        var parts = new List<object>
        {
            new Dictionary<string, object>
            {
                ["text"] = string.IsNullOrWhiteSpace(conversationHistory)
                    ? $"{systemPrompt}\n\n{prompt}"
                    : $"{systemPrompt}\n\nPrevious Conversation Context:\n{conversationHistory}\n\n{prompt}"
            }
        };

        if (!string.IsNullOrWhiteSpace(base64Image))
        {
            parts.Add(new Dictionary<string, object>
            {
                ["inlineData"] = new Dictionary<string, object>
                {
                    ["mimeType"] = mimeType,
                    ["data"] = base64Image
                }
            });
        }

        return new Dictionary<string, object>
        {
            ["generationConfig"] = new Dictionary<string, object>
            {
                ["responseMimeType"] = "application/json"
            },
            // Medical X-rays often false-trigger default safety filters; relax for legitimate clinical education use.
            ["safetySettings"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["category"] = "HARM_CATEGORY_HARASSMENT",
                    ["threshold"] = "BLOCK_NONE"
                },
                new Dictionary<string, object>
                {
                    ["category"] = "HARM_CATEGORY_HATE_SPEECH",
                    ["threshold"] = "BLOCK_NONE"
                },
                new Dictionary<string, object>
                {
                    ["category"] = "HARM_CATEGORY_SEXUALLY_EXPLICIT",
                    ["threshold"] = "BLOCK_NONE"
                },
                new Dictionary<string, object>
                {
                    ["category"] = "HARM_CATEGORY_DANGEROUS_CONTENT",
                    ["threshold"] = "BLOCK_NONE"
                }
            },
            ["contents"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["parts"] = parts
                }
            }
        };
    }

    private async Task<(string? base64, string? mimeType)> ResolveImageToBase64Async(string imageUrlOrBase64, CancellationToken ct)
    {
        // Accept raw base64 (our ImageProcessingService output) OR an HTTP(s) URL.
        if (imageUrlOrBase64.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            imageUrlOrBase64.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var client = _httpClientFactory.CreateClient(HttpClientName);
                using var req = new HttpRequestMessage(HttpMethod.Get, imageUrlOrBase64);
                using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

                if (resp.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    _logger.LogWarning(
                        "GeminiService image download failed. imageUrl={ImageUrl}, statusCode={StatusCode}",
                        imageUrlOrBase64,
                        resp.StatusCode);
                    return (null, null);
                }

                var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
                return (Convert.ToBase64String(bytes), MimeTypeJpeg);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to download image from URL (no Gemini call). imageUrl={ImageUrl}",
                    imageUrlOrBase64);
                return (null, null);
            }
        }

        // Allow a data URL input like data:image/jpeg;base64,AAAA...
        if (imageUrlOrBase64.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var match = Regex.Match(imageUrlOrBase64, @"^data:(?<mime>[^;]+);base64,(?<data>.+)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return (match.Groups["data"].Value, match.Groups["mime"].Value);
            }
        }

        // Treat as base64 directly.
        return (imageUrlOrBase64, MimeTypeJpeg);
    }

    private VisualQAResponseDto ParseGeminiResponse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            _logger.LogWarning("Gemini raw response was empty (stream or generateContent assembly).");
            return BuildFallbackClarificationDto(FallbackNoReliableInfoAnswer);
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(raw.Trim());
        }
        catch (JsonException ex)
        {
            var sanitized = SanitizeJsonCandidateText(raw);
            if (string.IsNullOrWhiteSpace(sanitized))
                throw new AiResponseFormatException("AI response was not valid JSON.", ex);
            try
            {
                doc = JsonDocument.Parse(sanitized);
            }
            catch (JsonException ex2)
            {
                throw new AiResponseFormatException("AI response JSON could not be parsed after sanitize.", ex2);
            }
        }

        using (doc)
        {
            return ParseGeminiResponseRoot(doc.RootElement);
        }
    }

    private VisualQAResponseDto ParseGeminiResponseRoot(JsonElement root)
    {

        // Safety filter / blocked prompts: Gemini returns promptFeedback.blockReason.
        if (root.TryGetProperty("promptFeedback", out var promptFeedback) &&
            promptFeedback.TryGetProperty("blockReason", out var blockReason) &&
            !string.IsNullOrWhiteSpace(blockReason.GetString()))
        {
            _logger.LogWarning("Gemini safety filter blocked the request: {BlockReason}", blockReason.GetString());
            return new VisualQAResponseDto
            {
                AnswerText = FallbackNoReliableInfoAnswer,
                SuggestedDiagnosis = null,
                DifferentialDiagnoses = null,
                KeyImagingFindings = null,
                ReflectiveQuestions = null,
                AiConfidenceScore = null,
                ErrorMessage = FallbackNoReliableInfoAnswer,
                ResponseKind = "refusal",
                Citations = new List<CitationItemDto>()
            };
        }

        // Some Gemini variants set finishReason="SAFETY" on the candidate.
        if (root.TryGetProperty("candidates", out var candidates) &&
            candidates.ValueKind == JsonValueKind.Array &&
            candidates.GetArrayLength() > 0)
        {
            var cand0 = candidates[0];
            if (cand0.TryGetProperty("finishReason", out var finishReason) &&
                string.Equals(finishReason.GetString(), "SAFETY", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Gemini safety filter blocked via finishReason=SAFETY.");
                return new VisualQAResponseDto
                {
                    AnswerText = FallbackNoReliableInfoAnswer,
                    SuggestedDiagnosis = null,
                    DifferentialDiagnoses = null,
                    KeyImagingFindings = null,
                    ReflectiveQuestions = null,
                    AiConfidenceScore = null,
                    ErrorMessage = FallbackNoReliableInfoAnswer,
                    ResponseKind = "refusal",
                    Citations = new List<CitationItemDto>()
                };
            }
        }

        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("diagnosis", out _) &&
            !root.TryGetProperty("candidates", out _))
        {
            return BuildVisualResponseFromValidatedCanonical(root);
        }

        var text = ExtractCandidateText(root);
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("Gemini returned empty text.");
            return new VisualQAResponseDto
            {
                AnswerText = FallbackNoReliableInfoAnswer,
                SuggestedDiagnosis = null,
                DifferentialDiagnoses = null,
                KeyImagingFindings = null,
                ReflectiveQuestions = null,
                AiConfidenceScore = null,
                ErrorMessage = FallbackNoReliableInfoAnswer,
                ResponseKind = "clarification",
                Citations = new List<CitationItemDto>()
            };
        }

        if (string.Equals(text.Trim(), InvalidImageNotXrayGuardrail, StringComparison.Ordinal))
        {
            return new VisualQAResponseDto
            {
                AnswerText = InvalidImageNotXrayGuardrail,
                SuggestedDiagnosis = null,
                DifferentialDiagnoses = null,
                KeyImagingFindings = null,
                ReflectiveQuestions = null,
                AiConfidenceScore = null,
                ErrorMessage = null,
                ResponseKind = "refusal",
                Citations = new List<CitationItemDto>()
            };
        }

        var sanitizedText = SanitizeJsonCandidateText(text);
        if (string.IsNullOrWhiteSpace(sanitizedText))
            throw new AiResponseFormatException("AI response did not contain a valid JSON object.");

        JsonDocument parsed;
        try
        {
            parsed = JsonDocument.Parse(sanitizedText);
        }
        catch (JsonException ex)
        {
            throw new AiResponseFormatException("AI response JSON could not be parsed.", ex);
        }

        using (parsed)
        {
            return BuildVisualResponseFromValidatedCanonical(parsed.RootElement);
        }
    }

    private VisualQAResponseDto BuildVisualResponseFromValidatedCanonical(JsonElement canonicalRoot)
    {
        var canonical = ValidateAndReadCanonicalResponse(canonicalRoot);

        if (string.Equals(canonical.Diagnosis.Trim(), InvalidImageNotXrayGuardrail, StringComparison.Ordinal))
        {
            return new VisualQAResponseDto
            {
                AnswerText = InvalidImageNotXrayGuardrail,
                SuggestedDiagnosis = null,
                DifferentialDiagnoses = null,
                KeyImagingFindings = null,
                ReflectiveQuestions = null,
                AiConfidenceScore = null,
                ErrorMessage = null,
                ResponseKind = "refusal",
                Citations = new List<CitationItemDto>()
            };
        }

        // Không gán AnswerText = diagnosis: SaveVisualQAMessagesAsync lưu Content = AnswerText;
        // trùng SuggestedDiagnosis làm qa_messages.content lặp chẩn đoán → FE/streaming/request history phình token & UI trùng.
        // Toàn bộ lâm sàng nằm ở SuggestedDiagnosis / KeyImagingFindings / …; narrative phẳng chỉ khi sau này có luồng riêng.
        return new VisualQAResponseDto
        {
            AnswerText = string.Empty,
            SuggestedDiagnosis = canonical.Diagnosis,
            DifferentialDiagnoses = canonical.DifferentialDiagnoses.Count == 0 ? null : canonical.DifferentialDiagnoses.ToList(),
            KeyImagingFindings = canonical.Findings.Count == 0 ? null : string.Join("\n- ", canonical.Findings),
            ReflectiveQuestions = canonical.ReflectiveQuestions.Count == 0 ? null : string.Join("\n- ", canonical.ReflectiveQuestions),
            AiConfidenceScore = null,
            ErrorMessage = null,
            ResponseKind = DetermineResponseKind(canonical.Diagnosis),
            Citations = canonical.Citations.ToList()
        };
    }

    private static string DetermineResponseKind(string diagnosis)
    {
        if (string.IsNullOrWhiteSpace(diagnosis))
            return "clarification";

        var text = diagnosis.Trim();
        if (text.Contains("not related to the musculoskeletal medical domain", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("not valid medical data", StringComparison.OrdinalIgnoreCase))
            return "refusal";
        if (string.Equals(text, NoContextAnswer, StringComparison.Ordinal) ||
            string.Equals(text, FallbackNoReliableInfoAnswer, StringComparison.Ordinal))
            return "clarification";

        // Khớp VisualQaSessionTurnsMapper: có chẩn đoán lâm sàng (JSON diagnosis) là analysis, kể cả khi listings rỗng.
        return "analysis";
    }

    private static VisualQaApiResponseDto ValidateAndReadCanonicalResponse(JsonElement result)
    {
        if (result.ValueKind != JsonValueKind.Object)
            throw new AiResponseFormatException("AI response root must be a JSON object.");

        if (!result.TryGetProperty("diagnosis", out var diagnosisEl) || diagnosisEl.ValueKind != JsonValueKind.String)
            throw new AiResponseFormatException("AI response must contain a string 'diagnosis' field.");

        var diagnosis = diagnosisEl.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(diagnosis))
            throw new AiResponseFormatException("AI response 'diagnosis' field cannot be empty.");

        var findings = ReadStrictStringArray(result, "findings");
        var differentialDiagnoses = ReadStrictStringArray(result, "differential_diagnoses");
        var reflectiveQuestions = ReadStrictStringArray(result, "reflective_questions");
        var citations = ReadStrictCitations(result);

        return new VisualQaApiResponseDto
        {
            CaseId = null,
            Diagnosis = diagnosis,
            Findings = findings,
            DifferentialDiagnoses = differentialDiagnoses,
            ReflectiveQuestions = reflectiveQuestions,
            Citations = citations
        };
    }

    private static IReadOnlyList<string> ReadStrictStringArray(JsonElement result, string propertyName)
    {
        if (!result.TryGetProperty(propertyName, out var property))
            throw new AiResponseFormatException($"AI response is missing '{propertyName}'.");

        if (property.ValueKind == JsonValueKind.Null)
            return Array.Empty<string>();

        if (property.ValueKind != JsonValueKind.Array)
            throw new AiResponseFormatException($"AI response field '{propertyName}' must be an array of strings.");

        var items = new List<string>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                throw new AiResponseFormatException($"AI response field '{propertyName}' must contain only strings.");

            var text = item.GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(text))
                items.Add(text);
        }

        return items;
    }

    private static IReadOnlyList<CitationItemDto> ReadStrictCitations(JsonElement result)
    {
        if (!result.TryGetProperty("citations", out var citationsEl))
            throw new AiResponseFormatException("AI response is missing 'citations'.");

        if (citationsEl.ValueKind == JsonValueKind.Null)
            return Array.Empty<CitationItemDto>();

        if (citationsEl.ValueKind != JsonValueKind.Array)
            throw new AiResponseFormatException("AI response field 'citations' must be an array.");

        var citations = new List<CitationItemDto>();
        foreach (var el in citationsEl.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Object)
                throw new AiResponseFormatException("Each citation must be an object.");

            if (!el.TryGetProperty("kind", out var kindEl) || kindEl.ValueKind != JsonValueKind.String)
                throw new AiResponseFormatException("Each citation must contain a string 'kind'.");
            if (!el.TryGetProperty("id", out var idEl) || idEl.ValueKind != JsonValueKind.String)
                throw new AiResponseFormatException("Each citation must contain a string 'id'.");

            var kind = kindEl.GetString();
            var idStr = idEl.GetString();
            if (!Guid.TryParse(idStr, out var citationId))
                throw new AiResponseFormatException("Each citation 'id' must be a valid UUID.");

            if (string.Equals(kind, "Doc", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kind, "doc", StringComparison.OrdinalIgnoreCase))
            {
                citations.Add(new CitationItemDto
                {
                    ChunkId = citationId,
                    Kind = "doc"
                });
            }
            else if (string.Equals(kind, "Case", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(kind, "case", StringComparison.OrdinalIgnoreCase))
            {
                citations.Add(new CitationItemDto
                {
                    ChunkId = Guid.Empty,
                    MedicalCaseId = citationId,
                    Kind = "case"
                });
            }
            else
            {
                throw new AiResponseFormatException("Citation 'kind' must be either 'Doc' or 'Case'.");
            }
        }

        return citations;
    }

    /// <summary>
    /// Gemini sometimes returns list-valued fields as JSON arrays instead of a single string.
    /// Accept <see cref="JsonValueKind.String"/>, <see cref="JsonValueKind.Null"/>, or <see cref="JsonValueKind.Array"/> (joined with newline + bullet).
    /// </summary>
    private static string ExtractCandidateText(JsonElement root)
    {
        if (root.TryGetProperty("candidates", out var candidates) &&
            candidates.ValueKind == JsonValueKind.Array &&
            candidates.GetArrayLength() > 0)
        {
            var cand0 = candidates[0];
            if (cand0.TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts) &&
                parts.ValueKind == JsonValueKind.Array &&
                parts.GetArrayLength() > 0)
            {
                var sb = new StringBuilder();
                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var textEl))
                        sb.Append(textEl.GetString() ?? string.Empty);
                }

                return sb.ToString();
            }
        }

        return string.Empty;
    }

    private static string SanitizeJsonCandidateText(string rawData)
    {
        if (string.IsNullOrWhiteSpace(rawData))
            return string.Empty;

        var cleaned = rawData
            .Replace("```json", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("```", string.Empty, StringComparison.Ordinal)
            .Trim();

        var start = cleaned.IndexOf('{');
        var end = cleaned.LastIndexOf('}');
        if (start >= 0 && end > start)
            cleaned = cleaned.Substring(start, end - start + 1);

        return cleaned.Trim();
    }

    private static bool IsTransient(System.Net.HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        // Retry on rate limit, timeouts and most 5xx.
        return statusCode == (System.Net.HttpStatusCode)429 ||
               statusCode == (System.Net.HttpStatusCode)408 ||
               (code >= 500 && code <= 599);
    }
}

