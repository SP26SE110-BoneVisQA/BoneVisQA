using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Quiz;
using BoneVisQA.Services.Services.AiQuizServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BoneVisQA.Services.Services.AiQuizServices;

public class AIQuizService : IAIQuizService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IQuizGeminiService _quizGemini;
    private readonly ILogger<AIQuizService> _logger;

    private const string QuizGenerationSystemPrompt =
        "YOU ARE AN EXPERT IN CREATING MUSCULOSKELETAL MEDICAL MULTIPLE-CHOICE QUESTIONS.\n" +
        "TASK: Generate high-quality multiple-choice questions on musculoskeletal imaging diagnosis.\n\n" +
        "STRICT RULES:\n" +
        "1. Each question must have 4 options (A, B, C, D)\n" +
        "2. There must be exactly 1 correct answer\n" +
        "3. Incorrect options must be plausible and potentially confusable\n" +
        "4. Questions must be based on the described X-ray, CT, or MRI findings\n" +
        "5. Respond in professional, accurate medical English\n" +
        "6. The correct answer must be A, B, C, or D (not answer text)\n\n" +
        "RETURN ONLY A JSON ARRAY in this format:\n" +
        "{\"questions\": [{\"questionText\": \"...\", \"optionA\": \"...\", \"optionB\": \"...\", \"optionC\": \"...\", \"optionD\": \"...\", \"correctAnswer\": \"A\"}]}\n" +
        "DO NOT add any text outside the JSON.";

    public AIQuizService(
        IUnitOfWork unitOfWork,
        IQuizGeminiService quizGemini,
        ILogger<AIQuizService> logger)
    {
        _unitOfWork = unitOfWork;
        _quizGemini = quizGemini;
        _logger = logger;
    }

    public async Task<AIQuizGenerationResultDto> GenerateQuizQuestionsAsync(
        string topic,
        int questionCount = 5,
        string? difficulty = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Lấy cases liên quan đến topic (title/mô tả/category/tags). Không required is_approved để tránh DB trống.
            var cases = await GetCasesByTopicAsync(topic, cancellationToken);
            List<AIQuizCaseInputDto> caseInfos;
            string prompt;
            string imageUrl;

            if (cases.Count > 0)
            {
                caseInfos = await GetCaseImageInfosAsync(cases, cancellationToken);
                prompt = BuildQuizGenerationPrompt(topic, caseInfos, questionCount, difficulty);
                imageUrl = caseInfos.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.ImageUrl))?.ImageUrl ?? string.Empty;
            }
            else
            {
                // None case trong DB: vẫn tạo câu hỏi theo chủ đề (ôn lý thuyết / hình ảnh chung)
                caseInfos = new List<AIQuizCaseInputDto>();
                prompt = BuildTopicOnlyQuizPrompt(topic, questionCount, difficulty);
                imageUrl = string.Empty;
            }

            var rawText = await _quizGemini.GenerateQuizAsync(
                prompt,
                string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl,
                cancellationToken);
            if (string.IsNullOrWhiteSpace(rawText))
            {
                return new AIQuizGenerationResultDto
                {
                    Success = false,
                    Message =
                        "Gemini returned no data. Check: (1) Gemini:ApiKeys configuration in appsettings/environment variables, " +
                        "(2) Gemini:ModelId and Gemini:BaseUrl (e.g., v1beta + gemini-2.0-flash), (3) API quota.",
                    Questions = new List<AIQuizQuestionDto>(),
                    Topic = topic,
                    Difficulty = difficulty
                };
            }

            var questions = ParseAIQuizResponse(rawText, caseInfos);

            var suffix = cases.Count == 0 ? " (topic-based, not linked to a specific case in the system)" : string.Empty;
            return new AIQuizGenerationResultDto
            {
                Success = questions.Count > 0,
                Message = questions.Count > 0
                    ? $"Generated {questions.Count} questions{suffix}"
                    : "Could not parse quiz JSON from Gemini. Try reducing the number of questions or changing the model.",
                Questions = questions,
                Topic = topic,
                Difficulty = difficulty
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating AI quiz questions for topic: {Topic}", topic);
            return new AIQuizGenerationResultDto
            {
                Success = false,
                Message = "An error occurred while generating questions: " + ex.Message
            };
        }
    }

    public async Task<AIQuizGenerationResultDto> SuggestQuestionsFromCasesAsync(
        List<AIQuizCaseInputDto> cases,
        int questionsPerCase = 2,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (cases.Count == 0)
            {
                return new AIQuizGenerationResultDto
                {
                    Success = false,
                    Message = "Please select at least 1 case"
                };
            }

            // 1. Lấy chi tiết cases từ database nếu chỉ có CaseId
            var caseDetails = await EnrichCasesAsync(cases, cancellationToken);

            // 2. Generate prompt cho AI
            var prompt = BuildCaseBasedQuizPrompt(caseDetails, questionsPerCase);

            var imageUrl = caseDetails.FirstOrDefault(c => !string.IsNullOrEmpty(c.ImageUrl))?.ImageUrl;
            var rawText = await _quizGemini.GenerateQuizAsync(prompt, imageUrl, cancellationToken);
            if (string.IsNullOrWhiteSpace(rawText))
            {
                return new AIQuizGenerationResultDto
                {
                    Success = false,
                    Message =
                        "Gemini returned no data. Check Gemini:ApiKeys, ModelId, BaseUrl, and quota.",
                    Questions = new List<AIQuizQuestionDto>()
                };
            }

            var questions = ParseAIQuizResponseWithCaseInfo(rawText, caseDetails);

            return new AIQuizGenerationResultDto
            {
                Success = questions.Count > 0,
                Message = questions.Count > 0
                    ? $"Suggested {questions.Count} questions from {cases.Count} cases"
                    : "Could not parse quiz JSON from Gemini.",
                Questions = questions
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error suggesting questions from cases");
            return new AIQuizGenerationResultDto
            {
                Success = false,
                Message = "An error occurred while suggesting questions: " + ex.Message
            };
        }
    }

    private async Task<List<AIQuizCaseInputDto>> GetCasesByTopicAsync(string topic, CancellationToken ct)
    {
        var t = topic.Trim();
        if (string.IsNullOrEmpty(t))
            return new List<AIQuizCaseInputDto>();

        var normalizedTopic = t.ToLowerInvariant();
        // Token đơn giản để khớp lỏng (vd "Long Bone Fractures" -> long, bone, fractures)
        var topicTokens = normalizedTopic
            .Split(new[] { ' ', ',', ';', '/', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(x => x.Length > 2)
            .Distinct()
            .ToList();

        var query = _unitOfWork.Context.MedicalCases
            .AsNoTracking()
            .Include(c => c.Category)
            .Include(c => c.MedicalImages)
            .Include(c => c.CaseTags)
            .ThenInclude(ctg => ctg.Tag)
            .Where(c => c.IsActive != false);

        var cases = await query
            .Where(c =>
                c.Title.ToLower().Contains(normalizedTopic) ||
                c.Description.ToLower().Contains(normalizedTopic) ||
                (c.SuggestedDiagnosis != null && c.SuggestedDiagnosis.ToLower().Contains(normalizedTopic)) ||
                (c.KeyFindings != null && c.KeyFindings.ToLower().Contains(normalizedTopic)) ||
                (c.Category != null && c.Category.Name.ToLower().Contains(normalizedTopic)) ||
                c.CaseTags.Any(ct => ct.Tag != null && ct.Tag.Name.ToLower().Contains(normalizedTopic)) ||
                (topicTokens.Count > 0 && topicTokens.Any(tok =>
                    c.Title.ToLower().Contains(tok) ||
                    c.Description.ToLower().Contains(tok) ||
                    (c.Category != null && c.Category.Name.ToLower().Contains(tok)) ||
                    (c.SuggestedDiagnosis != null && c.SuggestedDiagnosis.ToLower().Contains(tok)) ||
                    (c.KeyFindings != null && c.KeyFindings.ToLower().Contains(tok)) ||
                    c.CaseTags.Any(ct => ct.Tag != null && ct.Tag.Name.ToLower().Contains(tok)))))
            .OrderByDescending(c => c.IsApproved == true)
            .ThenByDescending(c => c.CreatedAt)
            .Take(10)
            .ToListAsync(ct);

        return cases.Select(c => new AIQuizCaseInputDto
        {
            CaseId = c.Id,
            CaseTitle = c.Title,
            CaseDescription = c.Description,
            KeyFindings = c.KeyFindings,
            SuggestedDiagnosis = c.SuggestedDiagnosis,
            Difficulty = c.Difficulty
        }).ToList();
    }

    private async Task<List<AIQuizCaseInputDto>> GetCaseImageInfosAsync(List<AIQuizCaseInputDto> cases, CancellationToken ct)
    {
        var caseIds = cases.Where(c => c.CaseId.HasValue).Select(c => c.CaseId.Value).ToList();

        var images = await _unitOfWork.Context.MedicalImages
            .AsNoTracking()
            .Where(img => caseIds.Contains(img.CaseId))
            .ToListAsync(ct);

        foreach (var c in cases)
        {
            if (c.CaseId.HasValue)
            {
                var image = images.FirstOrDefault(img => img.CaseId == c.CaseId);
                if (image != null)
                {
                    c.ImageUrl = image.ImageUrl;
                    c.Modality = image.Modality;
                }
            }
        }

        return cases;
    }

    private async Task<List<AIQuizCaseInputDto>> EnrichCasesAsync(List<AIQuizCaseInputDto> cases, CancellationToken ct)
    {
        var caseIds = cases.Where(c => c.CaseId.HasValue).Select(c => c.CaseId.Value).ToList();

        var dbCases = await _unitOfWork.Context.MedicalCases
            .AsNoTracking()
            .Include(c => c.MedicalImages)
            .Where(c => caseIds.Contains(c.Id))
            .ToListAsync(ct);

        var result = new List<AIQuizCaseInputDto>();

        foreach (var inputCase in cases)
        {
            var dbCase = dbCases.FirstOrDefault(c => c.Id == inputCase.CaseId);
            result.Add(new AIQuizCaseInputDto
            {
                CaseId = inputCase.CaseId ?? dbCase?.Id,
                CaseTitle = inputCase.CaseTitle ?? dbCase?.Title,
                CaseDescription = inputCase.CaseDescription ?? dbCase?.Description,
                KeyFindings = inputCase.KeyFindings ?? dbCase?.KeyFindings,
                SuggestedDiagnosis = inputCase.SuggestedDiagnosis ?? dbCase?.SuggestedDiagnosis,
                Difficulty = inputCase.Difficulty ?? dbCase?.Difficulty,
                ImageUrl = dbCase?.MedicalImages.FirstOrDefault()?.ImageUrl,
                Modality = dbCase?.MedicalImages.FirstOrDefault()?.Modality
            });
        }

        return result;
    }

    private string BuildQuizGenerationPrompt(string topic, List<AIQuizCaseInputDto> cases, int questionCount, string? difficulty)
    {
        var caseDescriptions = string.Join("\n\n", cases.Select((c, i) =>
            $"Case {i + 1}: {c.CaseTitle}\n" +
            $"Description: {c.CaseDescription}\n" +
            $"Symptoms: {c.KeyFindings ?? "None"}\n" +
            $"Suggested diagnosis: {c.SuggestedDiagnosis ?? "None"}"
        ));

        return
            $"{QuizGenerationSystemPrompt}\n\n" +
            $"TOPIC: {topic}\n" +
            $"NUMBER OF QUESTIONS TO GENERATE: {questionCount}\n" +
            $"{(string.IsNullOrEmpty(difficulty) ? "" : $"DIFFICULTY: {difficulty}\n")}\n\n" +
            $"CASE INFORMATION:\n{caseDescriptions}\n\n" +
            $"Generate {questionCount} multiple-choice questions based on the cases above.";
    }

    private static string BuildTopicOnlyQuizPrompt(string topic, int questionCount, string? difficulty)
    {
        return
            $"{QuizGenerationSystemPrompt}\n\n" +
            "NOTE: There are no specific cases in the database. Generate questions based on standard medical knowledge for this topic (anatomy, imaging diagnosis, treatment).\n\n" +
            $"TOPIC: {topic}\n" +
            $"NUMBER OF QUESTIONS: {questionCount}\n" +
            $"{(string.IsNullOrEmpty(difficulty) ? "" : $"DIFFICULTY: {difficulty}\n")}\n" +
            $"Generate exactly {questionCount} 4-option multiple-choice questions with the correct answer as A/B/C/D.";
    }

    private string BuildCaseBasedQuizPrompt(List<AIQuizCaseInputDto> cases, int questionsPerCase)
    {
        var caseDescriptions = string.Join("\n\n", cases.Select((c, i) =>
            $"Case {i + 1}: {c.CaseTitle}\n" +
            $"Description: {c.CaseDescription}\n" +
            $"Imaging modality: {c.Modality ?? "X-Ray"}\n" +
            $"Symptoms: {c.KeyFindings ?? "None"}\n" +
            $"Diagnosis: {c.SuggestedDiagnosis ?? "None"}"
        ));

        var totalQuestions = cases.Count * questionsPerCase;

        return
            $"{QuizGenerationSystemPrompt}\n\n" +
            $"NUMBER OF QUESTIONS TO GENERATE: {totalQuestions} ({questionsPerCase} per case)\n\n" +
            $"CASE INFORMATION:\n{caseDescriptions}\n\n" +
            $"Generate {totalQuestions} multiple-choice questions, {questionsPerCase} per case.";
    }

    private List<AIQuizQuestionDto> ParseAIQuizResponse(string? responseText, List<AIQuizCaseInputDto> cases)
    {
        var questions = new List<AIQuizQuestionDto>();

        if (string.IsNullOrWhiteSpace(responseText))
            return questions;

        try
        {
            responseText = StripMarkdownCodeFence(responseText.Trim());

            // Tìm JSON trong response
            var jsonStart = responseText.IndexOf('{');
            var jsonEnd = responseText.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = responseText.Substring(jsonStart, jsonEnd - jsonStart + 1);
                using var doc = JsonDocument.Parse(jsonStr);
                var root = doc.RootElement;

                JsonElement questionsArr;
                if (root.TryGetProperty("questions", out var qLower) && qLower.ValueKind == JsonValueKind.Array)
                    questionsArr = qLower;
                else if (root.TryGetProperty("Questions", out var qUpper) && qUpper.ValueKind == JsonValueKind.Array)
                    questionsArr = qUpper;
                else
                    return questions;

                foreach (var q in questionsArr.EnumerateArray())
                {
                    // Lấy imageUrl từ case tương ứng theo thứ tự (mỗi case gán cho questionsPerCase câu)
                    string? imageUrl = null;
                    Guid? caseId = null;
                    string? caseTitle = null;
                    if (cases.Count > 0)
                    {
                        var questionsPerCase = questionsArr.GetArrayLength() / Math.Max(cases.Count, 1);
                        var caseIndex = Math.Min((questions.Count) / Math.Max(questionsPerCase, 1), cases.Count - 1);
                        var questionCase = cases[caseIndex];
                        imageUrl = questionCase.ImageUrl;
                        caseId = questionCase.CaseId;
                        caseTitle = questionCase.CaseTitle;
                    }

                    var question = new AIQuizQuestionDto
                    {
                        QuestionText = GetStringProperty(q, "questionText"),
                        OptionA = GetStringProperty(q, "optionA"),
                        OptionB = GetStringProperty(q, "optionB"),
                        OptionC = GetStringProperty(q, "optionC"),
                        OptionD = GetStringProperty(q, "optionD"),
                        CorrectAnswer = GetStringProperty(q, "correctAnswer"),
                        Type = "MultipleChoice",
                        CaseId = caseId,
                        CaseTitle = caseTitle,
                        ImageUrl = imageUrl
                    };

                    if (!string.IsNullOrWhiteSpace(question.QuestionText))
                        questions.Add(question);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI quiz response. Snippet: {Snippet}",
                responseText.Length > 400 ? responseText[..400] : responseText);
        }

        return questions;
    }

    private static string StripMarkdownCodeFence(string raw)
    {
        if (!raw.StartsWith("```", StringComparison.Ordinal))
            return raw;
        var afterFirstLine = raw.IndexOf('\n');
        if (afterFirstLine < 0)
            return raw;
        var body = raw[(afterFirstLine + 1)..];
        var close = body.LastIndexOf("```", StringComparison.Ordinal);
        if (close >= 0)
            body = body[..close];
        return body.Trim();
    }

    private List<AIQuizQuestionDto> ParseAIQuizResponseWithCaseInfo(string? responseText, List<AIQuizCaseInputDto> cases)
    {
        var questions = ParseAIQuizResponse(responseText, cases);

        // Gán caseId và imageUrl cho từng câu hỏi dựa trên thứ tự (mỗi case gán questionsPerCase câu hỏi)
        if (cases.Count > 0 && questions.Count > 0)
        {
            var questionsPerCase = questions.Count / cases.Count;
            for (int i = 0; i < questions.Count; i++)
            {
                var caseIndex = Math.Min(i / Math.Max(questionsPerCase, 1), cases.Count - 1);
                var questionCase = cases[caseIndex];
                questions[i].CaseId = questionCase.CaseId;
                questions[i].CaseTitle = questionCase.CaseTitle;
                questions[i].ImageUrl = questionCase.ImageUrl;
            }
        }

        return questions;
    }

    private static string GetStringProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.String)
                return prop.GetString() ?? string.Empty;
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var n))
                return n.ToString();
        }

        var pascal = propertyName.Length > 0
            ? char.ToUpperInvariant(propertyName[0]) + propertyName[1..]
            : propertyName;
        if (!string.Equals(pascal, propertyName, StringComparison.Ordinal) &&
            element.TryGetProperty(pascal, out var prop2))
        {
            if (prop2.ValueKind == JsonValueKind.String)
                return prop2.GetString() ?? string.Empty;
            if (prop2.ValueKind == JsonValueKind.Number && prop2.TryGetInt32(out var n2))
                return n2.ToString();
        }

        return string.Empty;
    }
}
