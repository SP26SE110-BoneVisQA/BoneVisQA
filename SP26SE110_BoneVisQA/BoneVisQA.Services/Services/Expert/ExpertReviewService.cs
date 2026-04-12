using System.Linq;
using System.Text.Json;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Constants;
using BoneVisQA.Services.Exceptions;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Interfaces.Expert;
using BoneVisQA.Services.Models.Expert;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Services.Services.Expert;

public class ExpertReviewService : IExpertReviewService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly IRagExpertAnswerIndexingSignal _ragExpertAnswerIndexingSignal;

    /// <summary>
    /// Lecturer triage must have escalated; student must be enrolled in a class where this expert is assigned.
    /// </summary>
    private static IQueryable<CaseAnswer> QueryExpertScopedEscalatedQueue(IUnitOfWork uow, Guid expertId) =>
        uow.Context.CaseAnswers
            .AsNoTracking()
            .Where(a =>
                a.Status == CaseAnswerStatuses.EscalatedToExpert ||
                a.Status == CaseAnswerStatuses.Escalated)
            .Where(a =>
                uow.Context.ClassEnrollments.Any(e =>
                    e.StudentId == a.Question.StudentId &&
                    e.Class!.ExpertId == expertId));

    public ExpertReviewService(
        IUnitOfWork unitOfWork,
        INotificationService notificationService,
        IRagExpertAnswerIndexingSignal ragExpertAnswerIndexingSignal)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _ragExpertAnswerIndexingSignal = ragExpertAnswerIndexingSignal;
    }

    public async Task<IReadOnlyList<ExpertEscalatedAnswerDto>> GetEscalatedAnswersAsync(Guid expertId)
    {
        // Materialize answers first so Include(... ThenInclude Chunk.Doc) is not dropped by a final Select projection (avoids N+1 on documents).
        var answers = await QueryExpertScopedEscalatedQueue(_unitOfWork, expertId)
            .Include(a => a.Question)
                .ThenInclude(q => q.Student)
            .Include(a => a.Question)
                .ThenInclude(q => q.Case)
                    .ThenInclude(c => c!.MedicalImages)
            .Include(a => a.ExpertReviews)
            .Include(a => a.Citations)
                .ThenInclude(c => c.Chunk)
                    .ThenInclude(ch => ch.Doc)
            .OrderByDescending(a => a.EscalatedAt ?? a.Question.CreatedAt)
            .ToListAsync();

        var studentIds = answers.Select(a => a.Question.StudentId).Distinct().ToList();
        var enrollmentRows = await _unitOfWork.Context.ClassEnrollments
            .AsNoTracking()
            .Include(e => e.Class)
            .Where(e => studentIds.Contains(e.StudentId) && e.Class != null && e.Class.ExpertId == expertId)
            .OrderByDescending(e => e.EnrolledAt)
            .ToListAsync();

        var enrollmentByStudent = enrollmentRows
            .GroupBy(e => e.StudentId)
            .ToDictionary(g => g.Key, g => g.First());

        return answers.Select(a =>
        {
            enrollmentByStudent.TryGetValue(a.Question.StudentId, out var enrollment);
            var review = a.ExpertReviews.FirstOrDefault(r => r.ExpertId == expertId);
            return new ExpertEscalatedAnswerDto
            {
                AnswerId = a.Id,
                QuestionId = a.QuestionId,
                StudentId = a.Question.StudentId,
                StudentName = a.Question.Student?.FullName ?? string.Empty,
                StudentEmail = a.Question.Student?.Email ?? string.Empty,
                CaseId = a.Question.CaseId,
                CaseTitle = a.Question.Case?.Title ?? string.Empty,
                QuestionText = a.Question.QuestionText,
                CurrentAnswerText = a.AnswerText,
                StructuredDiagnosis = a.StructuredDiagnosis,
                DifferentialDiagnoses = a.DifferentialDiagnoses,
                KeyImagingFindings = a.KeyImagingFindings,
                ReflectiveQuestions = a.ReflectiveQuestions,
                Status = a.Status,
                EscalatedById = a.EscalatedById,
                EscalatedAt = a.EscalatedAt,
                AiConfidenceScore = a.AiConfidenceScore,
                ClassId = enrollment?.ClassId,
                ClassName = enrollment?.Class?.ClassName ?? string.Empty,
                ReviewNote = review?.ReviewNote,
                Citations = MapCitations(a.Citations),
                ImageUrl = ResolveQuestionImageUrl(a.Question),
                CustomCoordinates = a.Question.CustomCoordinates
            };
        }).ToList();
    }

    public Task<IReadOnlyList<ExpertEscalatedAnswerDto>> GetCaseAnswersAsync(Guid expertId)
        => GetEscalatedAnswersAsync(expertId);
    public async Task<ExpertEscalatedAnswerDto> ResolveEscalatedAnswerAsync(Guid expertId, Guid answerId, ResolveEscalatedAnswerRequestDto request)
    {
        var answer = await _unitOfWork.Context.CaseAnswers
            .Include(a => a.Question)
                .ThenInclude(q => q.Student)
            .Include(a => a.Question)
                .ThenInclude(q => q.Case)
                    .ThenInclude(c => c!.MedicalImages)
            .Include(a => a.ExpertReviews)
            .Include(a => a.Citations)
                .ThenInclude(c => c.Chunk)
                    .ThenInclude(ch => ch.Doc)
            .FirstOrDefaultAsync(a => a.Id == answerId)
            ?? throw new KeyNotFoundException("Không tìm thấy câu trả lời cần xử lý.");

        var enrollment = await _unitOfWork.Context.ClassEnrollments
            .Include(e => e.Class)
            .FirstOrDefaultAsync(e =>
                e.StudentId == answer.Question.StudentId &&
                e.Class.ExpertId == expertId);

        var existingReview = answer.ExpertReviews.FirstOrDefault(r => r.ExpertId == expertId);

        if (string.Equals(answer.Status, CaseAnswerStatuses.ExpertApproved, StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("Câu trả lời này đã được xử lý trước đó.");

        if (enrollment == null)
            throw new InvalidOperationException("Chuyên gia không có quyền xử lý câu trả lời này (sinh viên không thuộc lớp do bạn phụ trách).");

        if (!CaseAnswerStatuses.IsEscalatedToExpert(answer.Status))
            throw new ConflictException("Chỉ câu trả lời đã được giảng viên chuyển lên chuyên gia mới được xử lý tại đây.");

        answer.AnswerText = request.AnswerText;
        answer.StructuredDiagnosis = request.StructuredDiagnosis;
        answer.DifferentialDiagnoses = NormalizeDifferentialDiagnosesForStorage(request.DifferentialDiagnoses);
        answer.KeyImagingFindings = request.KeyImagingFindings;
        answer.ReflectiveQuestions = request.ReflectiveQuestions;
        answer.ReviewedById = expertId;
        answer.ReviewedAt = DateTime.UtcNow;
        answer.Status = CaseAnswerStatuses.ExpertApproved;

        if (existingReview == null)
        {
            existingReview = new ExpertReview
            {
                Id = Guid.NewGuid(),
                ExpertId = expertId,
                AnswerId = answer.Id,
                ReviewNote = request.ReviewNote,
                Action = "Approve",
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.ExpertReviewRepository.AddAsync(existingReview);
        }
        else
        {
            existingReview.ReviewNote = request.ReviewNote;
            existingReview.Action = "Approve";
            await _unitOfWork.ExpertReviewRepository.UpdateAsync(existingReview);
        }

        await _unitOfWork.CaseAnswerRepository.UpdateAsync(answer);
        await _unitOfWork.SaveAsync();

        await _notificationService.SendNotificationToUserAsync(
            answer.Question.StudentId,
            "Chuyên gia đã xử lý câu hỏi của bạn",
            "Câu trả lời đã được chuyên gia duyệt. Bạn có thể xem lại trong lịch sử câu hỏi.",
            "expert_review",
            $"/student/cases/history");

        await _ragExpertAnswerIndexingSignal.NotifyExpertApprovedForFutureIndexingAsync(answer.Id);

        return new ExpertEscalatedAnswerDto
        {
            AnswerId = answer.Id,
            QuestionId = answer.QuestionId,
            StudentId = answer.Question.StudentId,
            StudentName = answer.Question.Student?.FullName ?? string.Empty,
            StudentEmail = answer.Question.Student?.Email ?? string.Empty,
            CaseId = answer.Question.CaseId,
            CaseTitle = answer.Question.Case?.Title ?? string.Empty,
            QuestionText = answer.Question.QuestionText,
            CurrentAnswerText = answer.AnswerText,
            StructuredDiagnosis = answer.StructuredDiagnosis,
            DifferentialDiagnoses = answer.DifferentialDiagnoses,
            KeyImagingFindings = answer.KeyImagingFindings,
            ReflectiveQuestions = answer.ReflectiveQuestions,
            Status = answer.Status,
            EscalatedById = answer.EscalatedById,
            EscalatedAt = answer.EscalatedAt,
            AiConfidenceScore = answer.AiConfidenceScore,
            ClassId = enrollment?.ClassId,
            ClassName = enrollment?.Class?.ClassName ?? string.Empty,
            ReviewNote = existingReview.ReviewNote,
            Citations = MapCitations(answer.Citations),
            ImageUrl = ResolveQuestionImageUrl(answer.Question),
            CustomCoordinates = answer.Question.CustomCoordinates
        };
    }

    public async Task FlagChunkAsync(Guid expertId, Guid chunkId, FlagChunkRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidOperationException("Lý do flag chunk là bắt buộc.");

        var chunk = await _unitOfWork.Context.DocumentChunks.FirstOrDefaultAsync(ch => ch.Id == chunkId)
            ?? throw new KeyNotFoundException("Không tìm thấy document chunk.");

        var canReviewChunk = await _unitOfWork.Context.Citations
            .Where(c => c.ChunkId == chunkId)
            .AnyAsync(c =>
                (c.Answer.Status == CaseAnswerStatuses.EscalatedToExpert ||
                 c.Answer.Status == CaseAnswerStatuses.Escalated) &&
                _unitOfWork.Context.ClassEnrollments.Any(e =>
                    e.StudentId == c.Answer.Question.StudentId &&
                    e.Class!.ExpertId == expertId));

        if (!canReviewChunk)
            throw new InvalidOperationException("Chuyên gia không có quyền flag chunk này.");

        if (!chunk.IsFlagged)
        {
            chunk.IsFlagged = true;
            await _unitOfWork.DocumentChunkRepository.UpdateAsync(chunk);
            await _unitOfWork.SaveAsync();
        }
    }

    private static string? NormalizeDifferentialDiagnosesForStorage(JsonElement? value)
    {
        if (value == null)
            return null;

        var el = value.Value;
        return el.ValueKind switch
        {
            JsonValueKind.Undefined or JsonValueKind.Null => null,
            JsonValueKind.String => string.IsNullOrWhiteSpace(el.GetString()) ? null : el.GetString(),
            JsonValueKind.Array =>
                string.Join(
                    "\n",
                    el.EnumerateArray()
                        .Select(x =>
                            x.ValueKind == JsonValueKind.String ? (x.GetString() ?? string.Empty) : x.ToString())
                        .Where(s => !string.IsNullOrWhiteSpace(s))) is { Length: > 0 } joined
                    ? joined
                    : null,
            _ => el.ToString()
        };
    }

    private static string? ResolveQuestionImageUrl(StudentQuestion? question)
    {
        if (question == null)
            return null;
        if (!string.IsNullOrWhiteSpace(question.CustomImageUrl))
            return question.CustomImageUrl.Trim();

        var images = question.Case?.MedicalImages;
        if (images == null || images.Count == 0)
            return null;

        var url = images
            .OrderBy(m => m.CreatedAt ?? DateTime.MinValue)
            .ThenBy(m => m.Id)
            .Select(m => m.ImageUrl)
            .FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));
        return string.IsNullOrWhiteSpace(url) ? null : url.Trim();
    }

    private static List<ExpertCitationDto> MapCitations(IEnumerable<Citation> citations)
    {
        return citations
            .OrderBy(c => c.Chunk?.ChunkOrder ?? int.MaxValue)
            .Select(c => new ExpertCitationDto
            {
                ChunkId = c.ChunkId,
                SourceText = c.Chunk?.Content,
                ReferenceUrl = BuildCitationUrl(c.Chunk?.Doc?.FilePath),
                PageNumber = c.Chunk == null ? null : c.Chunk.ChunkOrder + 1
            })
            .ToList();
    }

    private static string? BuildCitationUrl(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return null;

        return filePath;
    }
}
