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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BoneVisQA.Services.Services.AiQuiz;

public class AIQuizService : IAIQuizService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGeminiService _geminiService;
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
        IGeminiService geminiService,
        ILogger<AIQuizService> logger)
    {
        _unitOfWork = unitOfWork;
        _geminiService = geminiService;
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
            // 1. Lấy cases liên quan đến topic từ database
            var cases = await GetCasesByTopicAsync(topic, cancellationToken);
            if (cases.Count == 0)
            {
                return new AIQuizGenerationResultDto
                {
                    Success = false,
                    Message = $"Không tìm thấy cases nào cho topic: {topic}"
                };
            }

            // 2. Lấy thông tin hình ảnh từ cases
            var caseInfos = await GetCaseImageInfosAsync(cases, cancellationToken);

            // 3. Tạo prompt cho AI
            var prompt = BuildQuizGenerationPrompt(topic, caseInfos, questionCount, difficulty);

            // 4. Gọi Gemini để tạo câu hỏi
            var imageUrl = caseInfos.FirstOrDefault()?.ImageUrl ?? string.Empty;
            var aiResponse = await _geminiService.GenerateMedicalAnswerAsync(prompt, imageUrl, cancellationToken);

            // 5. Parse kết quả
            var questions = ParseAIQuizResponse(aiResponse.AnswerText, cases);

            return new AIQuizGenerationResultDto
            {
                Success = questions.Count > 0,
                Message = questions.Count > 0 ? $"Đã tạo {questions.Count} câu hỏi" : "Không thể tạo câu hỏi",
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

            // 3. Gọi Gemini để tạo câu hỏi
            var imageUrl = caseDetails.FirstOrDefault(c => !string.IsNullOrEmpty(c.ImageUrl))?.ImageUrl ?? string.Empty;
            var aiResponse = await _geminiService.GenerateMedicalAnswerAsync(prompt, imageUrl, cancellationToken);

            // 4. Parse kết quả
            var questions = ParseAIQuizResponseWithCaseInfo(aiResponse.AnswerText, caseDetails);

            return new AIQuizGenerationResultDto
            {
                Success = questions.Count > 0,
                Message = questions.Count > 0 ? $"Đã gợi ý {questions.Count} câu hỏi từ {cases.Count} cases" : "Không thể tạo câu hỏi",
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
        var normalizedTopic = topic.Trim().ToLower();

        var cases = await _unitOfWork.Context.MedicalCases
            .AsNoTracking()
            .Include(c => c.Category)
            .Include(c => c.MedicalImages)
            .Where(c => c.IsActive != false && c.IsApproved == true)
            .Where(c =>
                c.Title.ToLower().Contains(normalizedTopic) ||
                (c.Category != null && c.Category.Name.ToLower().Contains(normalizedTopic)) ||
                c.Description.ToLower().Contains(normalizedTopic))
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
            // Tìm JSON trong response
            var jsonStart = responseText.IndexOf('{');
            var jsonEnd = responseText.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = responseText.Substring(jsonStart, jsonEnd - jsonStart + 1);
                using var doc = JsonDocument.Parse(jsonStr);
                var root = doc.RootElement;

                if (root.TryGetProperty("questions", out var questionsArr) && questionsArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var q in questionsArr.EnumerateArray())
                    {
                        var question = new AIQuizQuestionDto
                        {
                            QuestionText = GetStringProperty(q, "questionText"),
                            OptionA = GetStringProperty(q, "optionA"),
                            OptionB = GetStringProperty(q, "optionB"),
                            OptionC = GetStringProperty(q, "optionC"),
                            OptionD = GetStringProperty(q, "optionD"),
                            CorrectAnswer = GetStringProperty(q, "correctAnswer"),
                            Type = "MultipleChoice",
                            CaseId = cases.FirstOrDefault()?.CaseId,
                            CaseTitle = cases.FirstOrDefault()?.CaseTitle
                        };

                        if (!string.IsNullOrWhiteSpace(question.QuestionText))
                            questions.Add(question);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI quiz response");
        }

        return questions;
    }

    private List<AIQuizQuestionDto> ParseAIQuizResponseWithCaseInfo(string? responseText, List<AIQuizCaseInputDto> cases)
    {
        var questions = ParseAIQuizResponse(responseText, cases);

        // Gán caseId cho từng câu hỏi dựa trên thứ tự
        var questionsPerCase = questions.Count / cases.Count;
        for (int i = 0; i < questions.Count; i++)
        {
            var caseIndex = Math.Min(i / Math.Max(questionsPerCase, 1), cases.Count - 1);
            var questionCase = cases[caseIndex];
            questions[i].CaseId = questionCase.CaseId;
            questions[i].CaseTitle = questionCase.CaseTitle;
        }

        return questions;
    }

    private static string GetStringProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString() ?? string.Empty;
        return string.Empty;
    }
}
