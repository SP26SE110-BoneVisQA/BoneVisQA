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
        "BẠN LÀ CHUYÊN GIA TẠO CÂU HỎI TRẮC NGHIỆM Y KHOA CƠ XƯƠNG KHỚP.\n" +
        "NHIỆM VỤ: Tạo câu hỏi trắc nghiệm chất lượng cao về chẩn đoán hình ảnh cơ xương khớp.\n\n" +
        "QUY TẮC NGHIÊM NGẶT:\n" +
        "1. Mỗi câu hỏi phải có 4 lựa chọn (A, B, C, D)\n" +
        "2. Chỉ có 1 đáp án đúng duy nhất\n" +
        "3. Các đáp án sai phải hợp lý và có thể nhầm lẫn\n" +
        "4. Câu hỏi phải dựa trên hình ảnh X-ray, CT, MRI được mô tả\n" +
        "5. Trả lời bằng tiếng Việt, chuyên ngành y khoa chuẩn xác\n" +
        "6. Đáp án đúng phải là A, B, C hoặc D (không phải text đáp án)\n\n" +
        "CHỈ TRẢ VỀ MỘT MẢNG JSON với format:\n" +
        "{\"questions\": [{\"questionText\": \"...\", \"optionA\": \"...\", \"optionB\": \"...\", \"optionC\": \"...\", \"optionD\": \"...\", \"correctAnswer\": \"A\"}]}\n" +
        "KHÔNG thêm bất kỳ text nào khác ngoài JSON.";

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
            // 1. Lấy cases liên quan đến topic (title/mô tả/category/tags). Không bắt buộc is_approved để tránh DB trống.
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
                // Không có case trong DB: vẫn tạo câu hỏi theo chủ đề (ôn lý thuyết / hình ảnh chung)
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
                        "Gemini không trả về dữ liệu. Kiểm tra: (1) cấu hình Gemini:ApiKey trong appsettings / biến môi trường, " +
                        "(2) Gemini:ModelId và Gemini:BaseUrl (vd. v1beta + gemini-2.0-flash), (3) quota API.",
                    Questions = new List<AIQuizQuestionDto>(),
                    Topic = topic,
                    Difficulty = difficulty
                };
            }

            var questions = ParseAIQuizResponse(rawText, caseInfos);

            var suffix = cases.Count == 0 ? " (theo chủ đề, chưa gắn case cụ thể trong hệ thống)" : string.Empty;
            return new AIQuizGenerationResultDto
            {
                Success = questions.Count > 0,
                Message = questions.Count > 0
                    ? $"Đã tạo {questions.Count} câu hỏi{suffix}"
                    : "Không parse được JSON câu hỏi từ Gemini. Thử giảm số câu hoặc đổi model.",
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
                Message = "Đã xảy ra lỗi khi tạo câu hỏi: " + ex.Message
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
                    Message = "Vui lòng chọn ít nhất 1 case"
                };
            }

            // 1. Lấy chi tiết cases từ database nếu chỉ có CaseId
            var caseDetails = await EnrichCasesAsync(cases, cancellationToken);

            // 2. Tạo prompt cho AI
            var prompt = BuildCaseBasedQuizPrompt(caseDetails, questionsPerCase);

            var imageUrl = caseDetails.FirstOrDefault(c => !string.IsNullOrEmpty(c.ImageUrl))?.ImageUrl;
            var rawText = await _quizGemini.GenerateQuizAsync(prompt, imageUrl, cancellationToken);
            if (string.IsNullOrWhiteSpace(rawText))
            {
                return new AIQuizGenerationResultDto
                {
                    Success = false,
                    Message =
                        "Gemini không trả về dữ liệu. Kiểm tra Gemini:ApiKey, ModelId, BaseUrl và quota.",
                    Questions = new List<AIQuizQuestionDto>()
                };
            }

            var questions = ParseAIQuizResponseWithCaseInfo(rawText, caseDetails);

            return new AIQuizGenerationResultDto
            {
                Success = questions.Count > 0,
                Message = questions.Count > 0
                    ? $"Đã gợi ý {questions.Count} câu hỏi từ {cases.Count} cases"
                    : "Không parse được JSON câu hỏi từ Gemini.",
                Questions = questions
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error suggesting questions from cases");
            return new AIQuizGenerationResultDto
            {
                Success = false,
                Message = "Đã xảy ra lỗi khi gợi ý câu hỏi: " + ex.Message
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
            $"Mô tả: {c.CaseDescription}\n" +
            $"Triệu chứng: {c.KeyFindings ?? "Không có"}\n" +
            $"Chẩn đoán gợi ý: {c.SuggestedDiagnosis ?? "Không có"}"
        ));

        return
            $"{QuizGenerationSystemPrompt}\n\n" +
            $"CHỦ ĐỀ: {topic}\n" +
            $"SỐ CÂU HỎI CẦN TẠO: {questionCount}\n" +
            $"{(string.IsNullOrEmpty(difficulty) ? "" : $"ĐỘ KHÓ: {difficulty}\n")}\n\n" +
            $"THÔNG TIN CASES:\n{caseDescriptions}\n\n" +
            $"Tạo {questionCount} câu hỏi trắc nghiệm dựa trên các cases trên.";
    }

    private static string BuildTopicOnlyQuizPrompt(string topic, int questionCount, string? difficulty)
    {
        return
            $"{QuizGenerationSystemPrompt}\n\n" +
            "LƯU Ý: Hiện không có case cụ thể trong CSDL. Hãy tạo câu hỏi dựa trên kiến thức chuẩn y khoa về chủ đề (giải phẫu, chẩn đoán hình ảnh, điều trị) liên quan đến chủ đề.\n\n" +
            $"CHỦ ĐỀ: {topic}\n" +
            $"SỐ CÂU HỎI: {questionCount}\n" +
            $"{(string.IsNullOrEmpty(difficulty) ? "" : $"ĐỘ KHÓ: {difficulty}\n")}\n" +
            $"Tạo đúng {questionCount} câu hỏi trắc nghiệm 4 lựa chọn, đáp án đúng là A/B/C/D.";
    }

    private string BuildCaseBasedQuizPrompt(List<AIQuizCaseInputDto> cases, int questionsPerCase)
    {
        var caseDescriptions = string.Join("\n\n", cases.Select((c, i) =>
            $"Case {i + 1}: {c.CaseTitle}\n" +
            $"Mô tả: {c.CaseDescription}\n" +
            $"Hình ảnh: {c.Modality ?? "X-Ray"}\n" +
            $"Triệu chứng: {c.KeyFindings ?? "Không có"}\n" +
            $"Chẩn đoán: {c.SuggestedDiagnosis ?? "Không có"}"
        ));

        var totalQuestions = cases.Count * questionsPerCase;

        return
            $"{QuizGenerationSystemPrompt}\n\n" +
            $"SỐ CÂU HỎI CẦN TẠO: {totalQuestions} (mỗi case {questionsPerCase} câu)\n\n" +
            $"THÔNG TIN CASES:\n{caseDescriptions}\n\n" +
            $"Tạo {totalQuestions} câu hỏi trắc nghiệm, mỗi case {questionsPerCase} câu hỏi.";
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
