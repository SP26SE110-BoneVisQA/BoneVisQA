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
        "BẠN LÀ MỘT CHUYÊN GIA TRONG VIỆC TẠO CÂU HỎI TRẮC NGHIỆM Y KHOA CƠ XƯƠNG KHỚP.\n" +
        "NHIỆM VỤ: Tạo câu hỏi trắc nghiệm chất lượng cao về chẩn đoán hình ảnh cơ xương khớp bằng TIẾNG VIỆT.\n\n" +
        "YÊU CẦU QUAN TRỌNG VỀ NGÔN NGỮ:\n" +
        "- Tất cả câu hỏi, đáp án và giải thích PHẢI được viết bằng TIẾNG VIỆT\n" +
        "- Sử dụng thuật ngữ y khoa phổ biến tại Việt Nam\n" +
        "- VD: 'gãy xương', 'viêm khớp', 'thoái hóa', 'dị tậng bẩm sinh'\n\n" +
        "ĐỊNH DẠNG JSON BẮT BUỘC:\n" +
        "Bạn PHẢI trả về CHỈ một đối tượng JSON hợp lệ. Không có markdown, không có giải thích, không có văn bản trước hoặc sau JSON.\n\n" +
        "CẤU TRÚC JSON YÊU CẦU:\n" +
        "{\"questions\": [\n" +
        "  {\n" +
        "    \"questionText\": \"Câu hỏi bằng tiếng Việt ở đây\",\n" +
        "    \"type\": \"MultipleChoice\",\n" +
        "    \"optionA\": \"Đáp án A bằng tiếng Việt\",\n" +
        "    \"optionB\": \"Đáp án B bằng tiếng Việt\",\n" +
        "    \"optionC\": \"Đáp án C bằng tiếng Việt\",\n" +
        "    \"optionD\": \"Đáp án D bằng tiếng Việt\",\n" +
        "    \"correctAnswer\": \"A\",\n" +
        "    \"hint\": \"Gợi ý hữu ích cho sinh viên bằng tiếng Việt\",\n" +
        "    \"explanation\": \"Giải thích chi tiết tại sao đáp án đúng là đúng, bằng tiếng Việt. NÊN bao gồm kiến thức nền tảng và mẹo ghi nhớ.\"\n" +
        "  }\n" +
        "]}\n\n" +
        "QUY TẮC:\n" +
        "1. Mỗi câu hỏi phải có 4 đáp án: optionA, optionB, optionC, optionD\n" +
        "2. correctAnswer phải là một chữ cái: A, B, C, hoặc D\n" +
        "3. Tất cả giá trị chuỗi phải dùng dấu ngoặc kép đôi (không dùng ngoặc đơn)\n" +
        "4. KHÔNG bao gồm dấu phẩy ở cuối\n" +
        "5. KHÔNG bao gồm bất kỳ văn bản nào bên ngoài đối tượng JSON\n" +
        "6. Câu hỏi phải dựa trên các phát hiện X-quang, CT, hoặc MRI được mô tả\n" +
        "7. Các đáp án sai phải có thể tin được và dễ gây nhầm lẫn\n" +
        "8. Sử dụng tiếng Việt chuyên nghiệp, chính xác về y khoa";

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

    public async Task<List<AIQuizCaseInputDto>> GetAvailableCasesAsync(
        string? topic = null,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.Context.MedicalCases
            .AsNoTracking()
            .Include(c => c.Category)
            .Include(c => c.MedicalImages)
            .Include(c => c.CaseTags)
                .ThenInclude(ct => ct.Tag)
            .Where(c => c.IsActive != false)
            .Where(c => c.IsApproved == true);

        // Filter by topic if provided
        if (!string.IsNullOrWhiteSpace(topic))
        {
            var normalizedTopic = topic.Trim().ToLower();
            query = query.Where(c =>
                c.Title.ToLower().Contains(normalizedTopic) ||
                c.Description.ToLower().Contains(normalizedTopic) ||
                (c.SuggestedDiagnosis != null && c.SuggestedDiagnosis.ToLower().Contains(normalizedTopic)) ||
                (c.KeyFindings != null && c.KeyFindings.ToLower().Contains(normalizedTopic)) ||
                (c.Category != null && c.Category.Name.ToLower().Contains(normalizedTopic)) ||
                c.CaseTags.Any(ct => ct.Tag != null && ct.Tag.Name.ToLower().Contains(normalizedTopic)));
        }

        var cases = await query
            .OrderByDescending(c => c.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return cases.Select(c => new AIQuizCaseInputDto
        {
            CaseId = c.Id,
            CaseTitle = c.Title,
            CaseDescription = c.Description,
            KeyFindings = c.KeyFindings,
            SuggestedDiagnosis = c.SuggestedDiagnosis,
            Difficulty = c.Difficulty,
            ImageUrl = c.MedicalImages.FirstOrDefault()?.ImageUrl,
            Modality = c.MedicalImages.FirstOrDefault()?.Modality
        }).ToList();
    }

    public async Task<AIQuizGenerationResultDto> GenerateQuizFromCasesAsync(
        List<AIQuizCaseInputDto> cases,
        int questionCount = 5,
        string? difficulty = null,
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
            var prompt = BuildQuizGenerationPromptFromCases(caseDetails, questionCount, difficulty);

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

            // Build topic from cases
            var topicFromCases = string.Join(", ", caseDetails
                .Where(c => !string.IsNullOrWhiteSpace(c.CaseTitle))
                .Take(3)
                .Select(c => c.CaseTitle));

            return new AIQuizGenerationResultDto
            {
                Success = questions.Count > 0,
                Message = questions.Count > 0
                    ? $"AI generated {questions.Count} questions from {cases.Count} case(s)"
                    : "Could not parse quiz JSON from Gemini.",
                Questions = questions,
                Topic = topicFromCases,
                Difficulty = difficulty
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating quiz from cases");
            return new AIQuizGenerationResultDto
            {
                Success = false,
                Message = "An error occurred while generating questions: " + ex.Message
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
            $"Trường hợp {i + 1}: {c.CaseTitle}\n" +
            $"Mô tả: {c.CaseDescription}\n" +
            $"Triệu chứng: {c.KeyFindings ?? "Không có"}\n" +
            $"Chẩn đoán đề xuất: {c.SuggestedDiagnosis ?? "Không có"}"
        ));

        return
            $"{QuizGenerationSystemPrompt}\n\n" +
            $"CHỦ ĐỀ: {topic}\n" +
            $"SỐ CÂU HỎI CẦN TẠO: {questionCount}\n" +
            $"{(string.IsNullOrEmpty(difficulty) ? "" : $"ĐỘ KHÓ: {difficulty}\n")}\n\n" +
            $"THÔNG TIN CASE:\n{caseDescriptions}\n\n" +
            $"Tạo {questionCount} câu hỏi trắc nghiệm dựa trên các case trên. Tất cả câu hỏi phải bằng TIẾNG VIỆT.";
    }

    private static string BuildTopicOnlyQuizPrompt(string topic, int questionCount, string? difficulty)
    {
        return
            $"{QuizGenerationSystemPrompt}\n\n" +
            "LƯU Ý: Không có case cụ thể trong cơ sở dữ liệu. Tạo câu hỏi dựa trên kiến thức y khoa tiêu chuẩn cho chủ đề này (giải phẫu, chẩn đoán hình ảnh, điều trị).\n\n" +
            $"CHỦ ĐỀ: {topic}\n" +
            $"SỐ CÂU HỎI: {questionCount}\n" +
            $"{(string.IsNullOrEmpty(difficulty) ? "" : $"ĐỘ KHÓ: {difficulty}\n")}\n" +
            $"Tạo chính xác {questionCount} câu hỏi trắc nghiệm 4 đáp án với đáp án đúng là A/B/C/D. Tất cả phải bằng TIẾNG VIỆT.";
    }

    private string BuildCaseBasedQuizPrompt(List<AIQuizCaseInputDto> cases, int questionsPerCase)
    {
        var caseDescriptions = string.Join("\n\n", cases.Select((c, i) =>
            $"Trường hợp {i + 1}: {c.CaseTitle}\n" +
            $"Mô tả: {c.CaseDescription}\n" +
            $"Phương thức hình ảnh: {c.Modality ?? "X-Quang"}\n" +
            $"Triệu chứng: {c.KeyFindings ?? "Không có"}\n" +
            $"Chẩn đoán: {c.SuggestedDiagnosis ?? "Không có"}"
        ));

        var totalQuestions = cases.Count * questionsPerCase;

        return
            $"{QuizGenerationSystemPrompt}\n\n" +
            $"SỐ CÂU HỎI CẦN TẠO: {totalQuestions} ({questionsPerCase} câu mỗi case)\n\n" +
            $"THÔNG TIN CASE:\n{caseDescriptions}\n\n" +
            $"Tạo {totalQuestions} câu hỏi trắc nghiệm, {questionsPerCase} câu mỗi case. Tất cả phải bằng TIẾNG VIỆT.";
    }

    private string BuildQuizGenerationPromptFromCases(List<AIQuizCaseInputDto> cases, int questionCount, string? difficulty)
    {
        var caseDescriptions = string.Join("\n\n", cases.Select((c, i) =>
            $"Trường hợp {i + 1}: {c.CaseTitle}\n" +
            $"Mô tả: {c.CaseDescription}\n" +
            $"Phương thức hình ảnh: {c.Modality ?? "X-Quang"}\n" +
            $"Triệu chứng: {c.KeyFindings ?? "Không có"}\n" +
            $"Chẩn đoán đề xuất: {c.SuggestedDiagnosis ?? "Không có"}"
        ));

        return
            $"{QuizGenerationSystemPrompt}\n\n" +
            $"SỐ CÂU HỎI CẦN TẠO: {questionCount}\n" +
            $"{(string.IsNullOrEmpty(difficulty) ? "" : $"ĐỘ KHÓ: {difficulty}\n")}\n\n" +
            $"THÔNG TIN CASE ĐƯỢC CHỌN:\n{caseDescriptions}\n\n" +
            $"Tạo {questionCount} câu hỏi trắc nghiệm dựa trên các case trên. Tất cả câu hỏi phải bằng TIẾNG VIỆT.";
    }

    private List<AIQuizQuestionDto> ParseAIQuizResponse(string? responseText, List<AIQuizCaseInputDto> cases)
    {
        var questions = new List<AIQuizQuestionDto>();

        if (string.IsNullOrWhiteSpace(responseText))
        {
            _logger.LogWarning("ParseAIQuizResponse: responseText is null or whitespace");
            return questions;
        }

        _logger.LogInformation("ParseAIQuizResponse: Received text (first 500 chars): {Text}",
            responseText.Length > 500 ? responseText[..500] : responseText);

        try
        {
            responseText = StripMarkdownCodeFence(responseText.Trim());

            // Tìm JSON trong response - thử nhiều cách để tìm JSON
            string? jsonStr = TryExtractJson(responseText);

            if (jsonStr == null)
            {
                _logger.LogWarning("ParseAIQuizResponse: Could not find valid JSON in response. Snippet: {Snippet}",
                    responseText.Length > 300 ? responseText[..300] : responseText);
                return questions;
            }

            using var doc = JsonDocument.Parse(jsonStr);
            var root = doc.RootElement;

            JsonElement questionsArr;
            if (root.TryGetProperty("questions", out var qLower) && qLower.ValueKind == JsonValueKind.Array)
                questionsArr = qLower;
            else if (root.TryGetProperty("Questions", out var qUpper) && qUpper.ValueKind == JsonValueKind.Array)
                questionsArr = qUpper;
            else
            {
                _logger.LogWarning("ParseAIQuizResponse: No 'questions' or 'Questions' array found in JSON. Keys: {Keys}",
                    string.Join(", ", root.EnumerateObject().Select(p => p.Name)));
                return questions;
            }

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
                    Type = GetStringProperty(q, "type"),
                    CaseId = caseId,
                    CaseTitle = caseTitle,
                    ImageUrl = imageUrl,
                    Explanation = GetStringProperty(q, "explanation")
                };

                // Validate and set type defaults
                if (string.IsNullOrWhiteSpace(question.Type) ||
                    !Enum.TryParse<Repositories.Models.QuestionType>(question.Type, true, out var qt))
                {
                    question.Type = "MultipleChoice";
                }

                if (!string.IsNullOrWhiteSpace(question.Type))
                {
                    question.Type = question.Type.Trim();
                }

                if (!string.IsNullOrWhiteSpace(question.QuestionText))
                {
                    questions.Add(question);
                }
                else
                {
                    _logger.LogWarning("ParseAIQuizResponse: Skipping question with empty QuestionText at index {Index}", questions.Count);
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "JSON parse error in AI quiz response. Snippet: {Snippet}",
                responseText.Length > 400 ? responseText[..400] : responseText);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI quiz response. Snippet: {Snippet}",
                responseText.Length > 400 ? responseText[..400] : responseText);
        }

        return questions;
    }

    /// <summary>
    /// Thử nhiều cách để trích xuất JSON từ response
    /// </summary>
    private static string? TryExtractJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        text = text.Trim();

        // Cách 1: Tìm { ... } đầu tiên và cuối cùng
        var jsonStart = text.IndexOf('{');
        var jsonEnd = text.LastIndexOf('}');

        if (jsonStart >= 0 && jsonEnd > jsonStart)
        {
            var json = text.Substring(jsonStart, jsonEnd - jsonStart + 1);
            if (IsValidJson(json))
                return json;
        }

        // Cách 2: Thử tìm JSON array [...]
        var arrayStart = text.IndexOf('[');
        var arrayEnd = text.LastIndexOf(']');

        if (arrayStart >= 0 && arrayEnd > arrayStart)
        {
            var json = text.Substring(arrayStart, arrayEnd - arrayStart + 1);
            if (IsValidJson(json))
            {
                // Wrap trong object nếu là array thuần
                return "{\"questions\":" + json + "}";
            }
        }

        // Cách 3: Thử loại bỏ các ký tự không hợp lệ ở đầu
        var bracePos = text.IndexOf('{');
        if (bracePos > 0)
        {
            var trimmed = text[bracePos..];
            if (IsValidJson(trimmed))
                return trimmed;
        }

        return null;
    }

    /// <summary>
    /// Kiểm tra nhanh xem chuỗi có phải là JSON hợp lệ không
    /// </summary>
    private static bool IsValidJson(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            return true;
        }
        catch
        {
            return false;
        }
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
