using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Exceptions;
using BoneVisQA.Services.Interfaces.Expert;
using BoneVisQA.Services.Models.Expert;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Services.Services.Expert;

public class ExpertReviewService : IExpertReviewService
{
    private readonly IUnitOfWork _unitOfWork;

    public ExpertReviewService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<ExpertEscalatedAnswerDto>> GetEscalatedAnswersAsync(Guid expertId)
    {
        var query = _unitOfWork.Context.CaseAnswers
            .AsNoTracking()
            .Include(a => a.Question)
                .ThenInclude(q => q.Student)
            .Include(a => a.Question)
                .ThenInclude(q => q.Case)
            .Include(a => a.ExpertReviews)
            .Include(a => a.Citations)
                .ThenInclude(c => c.Chunk)
                    .ThenInclude(ch => ch.Doc)
            .Where(a => a.Status == "Escalated")
            .Where(a =>
                _unitOfWork.Context.ClassEnrollments.Any(e =>
                    e.StudentId == a.Question.StudentId &&
                    e.Class.ExpertId == expertId) ||
                a.ExpertReviews.Any(r => r.ExpertId == expertId))
            .Select(a => new
            {
                Answer = a,
                Enrollment = _unitOfWork.Context.ClassEnrollments
                    .Include(e => e.Class)
                    .Where(e => e.StudentId == a.Question.StudentId && e.Class.ExpertId == expertId)
                    .OrderByDescending(e => e.EnrolledAt)
                    .FirstOrDefault(),
                Review = a.ExpertReviews.FirstOrDefault(r => r.ExpertId == expertId)
            });

        var rows = await query.ToListAsync();

        return rows.Select(x => new ExpertEscalatedAnswerDto
        {
            AnswerId = x.Answer.Id,
            QuestionId = x.Answer.QuestionId,
            StudentId = x.Answer.Question.StudentId,
            StudentName = x.Answer.Question.Student?.FullName ?? string.Empty,
            StudentEmail = x.Answer.Question.Student?.Email ?? string.Empty,
            CaseId = x.Answer.Question.CaseId,
            CaseTitle = x.Answer.Question.Case?.Title ?? string.Empty,
            QuestionText = x.Answer.Question.QuestionText,
            CurrentAnswerText = x.Answer.AnswerText,
            StructuredDiagnosis = x.Answer.StructuredDiagnosis,
            DifferentialDiagnoses = x.Answer.DifferentialDiagnoses,
            KeyImagingFindings = x.Answer.KeyImagingFindings,
            ReflectiveQuestions = x.Answer.ReflectiveQuestions,
            Status = x.Answer.Status,
            EscalatedById = x.Answer.EscalatedById,
            EscalatedAt = x.Answer.EscalatedAt,
            AiConfidenceScore = x.Answer.AiConfidenceScore,
            ClassId = x.Enrollment?.ClassId,
            ClassName = x.Enrollment?.Class?.ClassName ?? string.Empty,
            ReviewNote = x.Review?.ReviewNote,
            Citations = MapCitations(x.Answer.Citations)
        }).ToList();
    }

    public async Task<ExpertEscalatedAnswerDto> ResolveEscalatedAnswerAsync(Guid expertId, Guid answerId, ResolveEscalatedAnswerRequestDto request)
    {
        var answer = await _unitOfWork.Context.CaseAnswers
            .Include(a => a.Question)
                .ThenInclude(q => q.Student)
            .Include(a => a.Question)
                .ThenInclude(q => q.Case)
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
        if (enrollment == null && existingReview == null)
            throw new InvalidOperationException("Chuyên gia không có quyền xử lý câu trả lời này.");

        // Concurrency guard: already resolved answers must not be resolved again.
        if (!string.Equals(answer.Status, "Escalated", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("Câu trả lời này đã được xử lý trước đó.");

        var resolvedStatus = DetermineResolvedStatus(answer, request);
        answer.AnswerText = request.AnswerText;
        answer.StructuredDiagnosis = request.StructuredDiagnosis;
        answer.DifferentialDiagnoses = request.DifferentialDiagnoses;
        answer.KeyImagingFindings = request.KeyImagingFindings;
        answer.ReflectiveQuestions = request.ReflectiveQuestions;
        answer.ReviewedById = expertId;
        answer.ReviewedAt = DateTime.UtcNow;
        answer.Status = resolvedStatus;

        if (existingReview == null)
        {
            existingReview = new ExpertReview
            {
                Id = Guid.NewGuid(),
                ExpertId = expertId,
                AnswerId = answer.Id,
                ReviewNote = request.ReviewNote,
                Action = "Resolved",
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.ExpertReviewRepository.AddAsync(existingReview);
        }
        else
        {
            existingReview.ReviewNote = request.ReviewNote;
            existingReview.Action = "Resolved";
            await _unitOfWork.ExpertReviewRepository.UpdateAsync(existingReview);
        }

        await _unitOfWork.CaseAnswerRepository.UpdateAsync(answer);
        await _unitOfWork.SaveAsync();

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
            Citations = MapCitations(answer.Citations)
        };
    }

    public async Task FlagChunkAsync(Guid expertId, Guid chunkId, FlagChunkRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidOperationException("Lý do flag chunk là bắt buộc.");

        var chunk = await _unitOfWork.Context.DocumentChunks
            .FirstOrDefaultAsync(ch => ch.Id == chunkId)
            ?? throw new KeyNotFoundException("Không tìm thấy document chunk.");

        var canReviewChunk = await _unitOfWork.Context.Citations
            .Where(c => c.ChunkId == chunkId)
            .AnyAsync(c =>
                (c.Answer.Status == "Escalated" || c.Answer.Status == "Approved" || c.Answer.Status == "Revised") &&
                (_unitOfWork.Context.ClassEnrollments.Any(e =>
                    e.StudentId == c.Answer.Question.StudentId &&
                    e.Class.ExpertId == expertId) ||
                 c.Answer.ExpertReviews.Any(r => r.ExpertId == expertId)));

        if (!canReviewChunk)
            throw new InvalidOperationException("Chuyên gia không có quyền flag chunk này.");

        if (!chunk.IsFlagged)
        {
            chunk.IsFlagged = true;
            await _unitOfWork.DocumentChunkRepository.UpdateAsync(chunk);
            await _unitOfWork.SaveAsync();
        }
    }

    private static string DetermineResolvedStatus(CaseAnswer answer, ResolveEscalatedAnswerRequestDto request)
    {
        var answerChanged =
            !string.Equals(answer.AnswerText ?? string.Empty, request.AnswerText ?? string.Empty, StringComparison.Ordinal) ||
            !string.Equals(answer.StructuredDiagnosis ?? string.Empty, request.StructuredDiagnosis ?? string.Empty, StringComparison.Ordinal) ||
            !string.Equals(answer.DifferentialDiagnoses ?? string.Empty, request.DifferentialDiagnoses ?? string.Empty, StringComparison.Ordinal) ||
            !string.Equals(answer.KeyImagingFindings ?? string.Empty, request.KeyImagingFindings ?? string.Empty, StringComparison.Ordinal) ||
            !string.Equals(answer.ReflectiveQuestions ?? string.Empty, request.ReflectiveQuestions ?? string.Empty, StringComparison.Ordinal);

        return answerChanged ? "Revised" : "Approved";
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
