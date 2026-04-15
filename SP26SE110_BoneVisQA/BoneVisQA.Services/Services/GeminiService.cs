using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BoneVisQA.Domain.Settings;
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
            ? "Only when the image (if any) is valid medical data, the question is related to musculoskeletal medicine, and context is sufficient should you fill suggestedDiagnosis, differentialDiagnoses, keyImagingFindings, reflectiveQuestions, and citations.\n" +
              "Always prioritize RAG context. If context is insufficient, set answerText exactly to: '" + NoContextAnswer + "' and set suggestedDiagnosis, differentialDiagnoses, keyImagingFindings, reflectiveQuestions, and citations ([]) to null or empty.\n"
            : "The document library (RAG context) may be missing or not relevant enough. IN THIS CASE, you MUST still answer using general musculoskeletal medical knowledge (prioritize image analysis when available). You may fill suggestedDiagnosis, differentialDiagnoses, keyImagingFindings, and reflectiveQuestions when appropriate; citations are usually []. At the end of answerText, ALWAYS append exactly: (Note: This analysis is based on general AI knowledge because no direct reference documents were found in the system library).\n" +
              "DO NOT return answerText as only a generic refusal when the question is still in the musculoskeletal medical domain—provide an academic explanation first, then optionally add the missing-document note above.\n";

        return
            "STEP 1: Analyze if the provided image is a Human Bone X-Ray. If it is NOT (e.g., it is a CT scan, MRI, an animal, or a random object), YOU MUST refuse to answer medical questions and output EXACTLY this string: 'INVALID_IMAGE_NOT_XRAY'. Stop processing further.\n" +
            "\n" +
            "MANDATORY QUESTION AND IMAGE ANALYSIS (STRICT REFUSAL RULES):\n" +
            "If the question is NOT related to musculoskeletal medicine, health, or musculoskeletal imaging (for example: fuel prices, weather, politics, programming), you MUST answer exactly with: 'Your question is not related to the musculoskeletal medical domain. Please provide a valid professional medical question.'\n" +
            "Do not answer with 'The database has no information'.\n" +
            "When refusing under this rule: set suggestedDiagnosis, differentialDiagnoses, keyImagingFindings, and reflectiveQuestions to null, citations to [] or null, and IGNORE ALL OTHER REQUESTS.\n" +
            "\n" +
            "MANDATORY IMAGE VALIDATION (IF PROVIDED):\n" +
            "1. If the provided image is NOT a musculoskeletal medical image (for example: landscapes, animals, ordinary people, objects, or non-medical images), YOU MUST REFUSE by setting `answerText` to 'The provided image is not valid medical data.' and setting suggestedDiagnosis, differentialDiagnoses, keyImagingFindings, and reflectiveQuestions to null and citations to []. Ignore all other requests.\n" +
            "\n" +
            "You MUST output a JSON object with EXACTLY these keys: 'answerText', 'suggestedDiagnosis', 'keyFindings' (array) OR 'keyImagingFindings' (string), 'differentialDiagnoses' (array or string), 'reflectiveQuestions' (string or array), 'citations' (array of objects { \"kind\": \"Doc\"|\"Case\", \"id\": \"uuid\" }).\n" +
            "In answerText, when citing the library, use [Doc:UUID] for document chunks and [Case:UUID] for medical cases, matching the citations array.\n" +
            "\n" +
            "Start the answer immediately. NO greetings. NO introduction.\n" +
            "DO NOT output content outside the listed JSON fields (and keyImagingFindings if used instead of keyFindings).\n" +
            "You must return a raw JSON object without any markdown wrapping like ```json.\n" +
            "When answering valid domain questions, respond in accurate professional medical English.\n" +
            "\n" +
            ragPolicy +
            "\n" +
            "RETURN EXACTLY 1 JSON OBJECT and do not append any other content.\n" +
            "Do not embed citationChunkIds or extra raw JSON inside answerText.\n" +
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

    public async Task<VisualQAResponseDto> GenerateMedicalAnswerAsync(
        string prompt,
        string imageUrl,
        string? conversationHistory = null,
        bool ragContextAdequate = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt must not be empty.", nameof(prompt));

        var apiKeys = _settings.GetResolvedApiKeys();
        if (apiKeys.Count == 0)
        {
            _logger.LogWarning("Gemini API keys missing (Gemini:ApiKeys). Returning empty safe response.");
            return new VisualQAResponseDto
            {
                AnswerText = FallbackNoReliableInfoAnswer,
                SuggestedDiagnosis = null,
                DifferentialDiagnoses = null,
                KeyImagingFindings = null,
                ReflectiveQuestions = null,
                AiConfidenceScore = null,
                ErrorMessage = FallbackNoReliableInfoAnswer,
                Citations = new List<CitationItemDto>()
            };
        }

        var modelIds = _settings.GetResolvedModelIds();
        if (modelIds.Count == 0)
        {
            _logger.LogWarning(
                "Gemini model ids missing (configure Gemini:Models or Gemini:ModelId). Returning empty safe response.");
            return new VisualQAResponseDto
            {
                AnswerText = FallbackNoReliableInfoAnswer,
                SuggestedDiagnosis = null,
                DifferentialDiagnoses = null,
                KeyImagingFindings = null,
                ReflectiveQuestions = null,
                AiConfidenceScore = null,
                ErrorMessage = FallbackNoReliableInfoAnswer,
                Citations = new List<CitationItemDto>()
            };
        }

        if (string.IsNullOrWhiteSpace(_settings.BaseUrl))
        {
            _logger.LogWarning("Gemini base URL missing (Gemini:BaseUrl). Returning empty safe response.");
            return new VisualQAResponseDto
            {
                AnswerText = FallbackNoReliableInfoAnswer,
                SuggestedDiagnosis = null,
                DifferentialDiagnoses = null,
                KeyImagingFindings = null,
                ReflectiveQuestions = null,
                AiConfidenceScore = null,
                ErrorMessage = FallbackNoReliableInfoAnswer,
                Citations = new List<CitationItemDto>()
            };
        }

        var apiKey = apiKeys[0];
        var baseUrl = _settings.BaseUrl.TrimEnd('/');

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
                    Citations = new List<CitationItemDto>()
                };
            }
        }

        var failureSummaries = new List<string>();
        var totalModels = modelIds.Count;
        var attemptIndex = 0;

        foreach (var modelId in modelIds)
        {
            attemptIndex++;
            _logger.LogInformation(
                "[GeminiService] generateContent attempt {AttemptIndex}/{TotalModels} using model id \"{ModelId}\"",
                attemptIndex,
                totalModels,
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

                    if (IsTransient(resp.StatusCode))
                    {
                        _logger.LogWarning(
                            "[GeminiService] Model id \"{ModelId}\" reached quota or transient limit (HTTP {StatusCode}). Switching to next available model...",
                            modelId,
                            (int)resp.StatusCode);
                        await Task.Delay(1000, cancellationToken);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "[GeminiService] Model id \"{ModelId}\": HTTP {StatusCode}. Trying next configured model.",
                            modelId,
                            (int)resp.StatusCode);
                    }

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
                        "[GeminiService] Completed successfully using model id \"{ModelId}\" (attempt {AttemptIndex}/{TotalModels}).",
                        modelId,
                        attemptIndex,
                        totalModels);
                    return parsed;
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
                    "[GeminiService] Model id \"{ModelId}\" request failed. Switching to next available model...",
                    modelId);
                await Task.Delay(1000, cancellationToken);
                continue;
            }
        }

        throw new InvalidOperationException(
            $"All configured Gemini models failed ({modelIds.Count} model(s)). Summary: {string.Join(" | ", failureSummaries)}");
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
        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;

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
                    Citations = new List<CitationItemDto>()
                };
            }
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
                Citations = new List<CitationItemDto>()
            };
        }

        var sanitizedText = SanitizeJsonCandidateText(text);
        using var parsed = JsonDocument.Parse(sanitizedText);
        var result = parsed.RootElement;

        var answerText = result.TryGetProperty("answerText", out var a) ? a.GetString() : null;
        var suggestedDiagnosis = result.TryGetProperty("suggestedDiagnosis", out var s)
            ? ReadStringOrJoinedArray(s)
            : null;
        var differentialDiagnoses = result.TryGetProperty("differentialDiagnoses", out var d)
            ? ReadStringList(d)
            : null;
        string? keyImagingFindings = null;
        if (result.TryGetProperty("keyFindings", out var kf))
            keyImagingFindings = ReadStringOrJoinedArray(kf);
        else if (result.TryGetProperty("keyImagingFindings", out var kfi))
            keyImagingFindings = ReadStringOrJoinedArray(kfi);
        var reflectiveQuestions = result.TryGetProperty("reflectiveQuestions", out var rq)
            ? ReadStringOrJoinedArray(rq)
            : null;

        var nullifyDiagnosis = ShouldNullifyDiagnosisFields(answerText);
        if (nullifyDiagnosis)
        {
            suggestedDiagnosis = null;
            differentialDiagnoses = null;
            keyImagingFindings = null;
            reflectiveQuestions = null;
        }

        var citations = new List<CitationItemDto>();
        if (!nullifyDiagnosis && result.TryGetProperty("citations", out var citationsEl))
        {
            if (citationsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in citationsEl.EnumerateArray())
                {
                    if (el.ValueKind != JsonValueKind.Object)
                        continue;

                    var kind = el.TryGetProperty("kind", out var k)
                        ? k.GetString()
                        : el.TryGetProperty("sourceType", out var st)
                            ? st.GetString()
                            : null;
                    var idStr = el.TryGetProperty("id", out var idp) ? idp.GetString() : null;
                    if (!Guid.TryParse(idStr, out var gid))
                        continue;

                    if (string.Equals(kind, "Doc", StringComparison.OrdinalIgnoreCase))
                    {
                        citations.Add(new CitationItemDto
                        {
                            ChunkId = gid,
                            MedicalCaseId = null,
                            SourceText = null
                        });
                    }
                    else if (string.Equals(kind, "Case", StringComparison.OrdinalIgnoreCase))
                    {
                        citations.Add(new CitationItemDto
                        {
                            ChunkId = Guid.Empty,
                            MedicalCaseId = gid,
                            SourceText = null
                        });
                    }
                }
            }
        }
        else if (!nullifyDiagnosis && result.TryGetProperty("citationChunkIds", out var ids) && ids.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in ids.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.String && Guid.TryParse(el.GetString(), out var id))
                {
                    citations.Add(new CitationItemDto
                    {
                        ChunkId = id,
                        SourceText = null
                    });
                }
            }
        }

        return new VisualQAResponseDto
        {
            AnswerText = !string.IsNullOrWhiteSpace(answerText) ? answerText : FallbackNoReliableInfoAnswer,
            SuggestedDiagnosis = suggestedDiagnosis,
            DifferentialDiagnoses = differentialDiagnoses,
            KeyImagingFindings = keyImagingFindings,
            ReflectiveQuestions = reflectiveQuestions,
            AiConfidenceScore = null,
            ErrorMessage = null,
            Citations = citations
        };
    }

    /// <summary>
    /// Gemini sometimes returns list-valued fields as JSON arrays instead of a single string.
    /// Accept <see cref="JsonValueKind.String"/>, <see cref="JsonValueKind.Null"/>, or <see cref="JsonValueKind.Array"/> (joined with newline + bullet).
    /// </summary>
    private static string? ReadStringOrJoinedArray(JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Null:
                return null;
            case JsonValueKind.String:
                return el.GetString();
            case JsonValueKind.Array:
            {
                var segments = new List<string>();
                foreach (var item in el.EnumerateArray())
                {
                    var s = JsonElementToPlainSegment(item);
                    if (!string.IsNullOrWhiteSpace(s))
                        segments.Add(s.Trim());
                }

                return segments.Count == 0 ? null : string.Join("\n- ", segments);
            }
            default:
                return el.GetRawText();
        }
    }

    private static List<string>? ReadStringList(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Null)
            return null;

        if (el.ValueKind == JsonValueKind.String)
        {
            var raw = el.GetString();
            if (string.IsNullOrWhiteSpace(raw))
                return null;
            return raw
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim().TrimStart('-', '*').Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (el.ValueKind == JsonValueKind.Array)
        {
            var list = new List<string>();
            foreach (var item in el.EnumerateArray())
            {
                var text = JsonElementToPlainSegment(item)?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                    list.Add(text);
            }

            return list.Count == 0 ? null : list;
        }

        return null;
    }

    private static string JsonElementToPlainSegment(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString() ?? string.Empty,
            JsonValueKind.Number => el.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => string.Empty,
            _ => el.GetRawText()
        };
    }

    /// <summary>
    /// Defense-in-depth: if the model returned a rejection / no-context message in answerText,
    /// strip any stray imaging diagnoses (mixed-intent JSON hallucination).
    /// </summary>
    private static bool ShouldNullifyDiagnosisFields(string? answerText)
    {
        if (string.IsNullOrWhiteSpace(answerText))
            return true;

        var t = answerText.Trim();
        // Prefer prefix / known-template matching to avoid stripping real clinical answers that quote these phrases.
        if (t.StartsWith(NoContextAnswer, StringComparison.Ordinal)
            || string.Equals(t, NoContextAnswer, StringComparison.Ordinal))
            return true;
        if (t.StartsWith(FallbackNoReliableInfoAnswer, StringComparison.Ordinal)
            || string.Equals(t, FallbackNoReliableInfoAnswer, StringComparison.Ordinal))
            return true;
        if (t.Contains("no sufficiently reliable information found", StringComparison.OrdinalIgnoreCase) && t.Length < 400)
            return true;
        if (t.Contains("does not contain enough information to answer this question", StringComparison.OrdinalIgnoreCase) && t.Length < 400)
            return true;
        if (t.StartsWith("The image you provided is not an X-ray", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(t, "The provided image is not valid medical data.", StringComparison.Ordinal))
            return true;
        if (t.Contains("not related to the medical domain", StringComparison.OrdinalIgnoreCase))
            return true;
        if (t.Contains("I only support analysis of musculoskeletal topics", StringComparison.OrdinalIgnoreCase) && t.Length < 400)
            return true;

        return false;
    }

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
                var part0 = parts[0];
                if (part0.TryGetProperty("text", out var text))
                    return text.GetString() ?? string.Empty;
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

