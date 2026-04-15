using System.Linq;
using System.Text.Json;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Constants;
using BoneVisQA.Services.Exceptions;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Interfaces.Expert;
using BoneVisQA.Services.Models.Expert;
using BoneVisQA.Services.Services;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Services.Services.Expert;

public class ExpertReviewService : IExpertReviewService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly IRagExpertAnswerIndexingSignal _ragExpertAnswerIndexingSignal;

    /// <summary>
    /// Expert queue sourced from Visual QA sessions escalated by lecturer.
    /// </summary>
    private static IQueryable<VisualQASession> QueryExpertScopedEscalatedQueue(IUnitOfWork uow, Guid expertId) =>
        uow.Context.VisualQaSessions
            .AsNoTracking()
            .Where(s => s.Status == "EscalatedToExpert")
            .Where(s =>
                uow.Context.ClassEnrollments.Any(e =>
                    e.StudentId == s.StudentId &&
                    e.Class != null &&
                    e.Class.ExpertId == expertId));

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
        var sessions = await QueryExpertScopedEscalatedQueue(_unitOfWork, expertId)
            .AsSplitQuery()
            .Include(s => s.Student)
            .Include(s => s.Case!)
                .ThenInclude(c => c.MedicalImages)
            .Include(s => s.Image)
            .Include(s => s.Messages)
                .ThenInclude(m => m.Citations)
                    .ThenInclude(c => c.Chunk)
                        .ThenInclude(ch => ch.Doc)
            .Include(s => s.ExpertReviews)
            .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
            .ToListAsync();

        var studentIds = sessions.Select(s => s.StudentId).Distinct().ToList();
        var enrollmentRows = await _unitOfWork.Context.ClassEnrollments
            .AsNoTracking()
            .Include(e => e.Class)
            .Where(e => studentIds.Contains(e.StudentId) && e.Class != null && e.Class.ExpertId == expertId)
            .OrderByDescending(e => e.EnrolledAt)
            .ToListAsync();

        var enrollmentByStudent = enrollmentRows
            .GroupBy(e => e.StudentId)
            .ToDictionary(g => g.Key, g => g.First());

        return sessions.Select(s =>
        {
            enrollmentByStudent.TryGetValue(s.StudentId, out var enrollment);
            var review = s.ExpertReviews.FirstOrDefault(r => r.ExpertId == expertId);
            var orderedMessages = s.Messages
                .OrderBy(m => m.CreatedAt)
                .ThenBy(m => m.Id)
                .ToList();
            var userMessage = orderedMessages.FirstOrDefault(m => m.Role == "User");
            var latestAssistant = orderedMessages.LastOrDefault(m => m.Role == "Assistant");

            return new ExpertEscalatedAnswerDto
            {
                AnswerId = s.Id,
                QuestionId = userMessage?.Id ?? Guid.Empty,
                StudentId = s.StudentId,
                StudentName = s.Student?.FullName ?? string.Empty,
                StudentEmail = s.Student?.Email ?? string.Empty,
                CaseId = s.CaseId,
                CaseTitle = s.Case?.Title ?? string.Empty,
                QuestionText = userMessage?.Content ?? string.Empty,
                CurrentAnswerText = latestAssistant?.Content,
                StructuredDiagnosis = latestAssistant?.SuggestedDiagnosis,
                DifferentialDiagnoses = latestAssistant?.DifferentialDiagnoses,
                KeyImagingFindings = latestAssistant?.KeyImagingFindings,
                ReflectiveQuestions = latestAssistant?.ReflectiveQuestions,
                Status = s.Status,
                EscalatedById = s.LecturerId,
                EscalatedAt = s.UpdatedAt ?? s.CreatedAt,
                AiConfidenceScore = latestAssistant?.AiConfidenceScore,
                ClassId = enrollment?.ClassId,
                ClassName = enrollment?.Class?.ClassName ?? string.Empty,
                ReviewNote = review?.ReviewNote,
                PromotedCaseId = s.PromotedCaseId,
                Citations = MapCitations(latestAssistant?.Citations ?? Enumerable.Empty<Citation>()),
                ImageUrl = ResolveSessionImageUrl(s),
                CustomCoordinates = userMessage?.Coordinates
            };
        }).ToList();
    }

    public Task<IReadOnlyList<ExpertEscalatedAnswerDto>> GetCaseAnswersAsync(Guid expertId)
        => GetEscalatedAnswersAsync(expertId);

    public async Task<ExpertEscalatedAnswerDto> RespondToSessionAsync(Guid expertId, Guid sessionId, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("Nội dung phản hồi của chuyên gia là bắt buộc.");

        var session = await _unitOfWork.Context.VisualQaSessions
            .Include(s => s.Student)
            .Include(s => s.Case!)
                .ThenInclude(c => c.MedicalImages)
            .Include(s => s.Image)
            .Include(s => s.Messages)
                .ThenInclude(m => m.Citations)
                    .ThenInclude(c => c.Chunk)
                        .ThenInclude(ch => ch.Doc)
            .FirstOrDefaultAsync(s => s.Id == sessionId)
            ?? throw new KeyNotFoundException("Không tìm thấy phiên hỏi đáp.");

        var enrollment = await _unitOfWork.Context.ClassEnrollments
            .Include(e => e.Class)
            .FirstOrDefaultAsync(e =>
                e.StudentId == session.StudentId &&
                e.Class.ExpertId == expertId);

        if (enrollment == null)
            throw new InvalidOperationException("Chuyên gia không có quyền phản hồi phiên hỏi đáp này.");

        var now = DateTime.UtcNow;
        var expertMessage = new QAMessage
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Role = "Expert",
            Content = content.Trim(),
            CreatedAt = now
        };

        await _unitOfWork.Context.QaMessages.AddAsync(expertMessage);
        session.Status = "Active";
        session.ExpertId = expertId;
        session.UpdatedAt = now;
        await _unitOfWork.SaveAsync();

        var orderedMessages = session.Messages
            .Append(expertMessage)
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .ToList();
        var userMessage = orderedMessages.FirstOrDefault(m => m.Role == "User");

        return new ExpertEscalatedAnswerDto
        {
            AnswerId = session.Id,
            QuestionId = userMessage?.Id ?? Guid.Empty,
            StudentId = session.StudentId,
            StudentName = session.Student?.FullName ?? string.Empty,
            StudentEmail = session.Student?.Email ?? string.Empty,
            CaseId = session.CaseId,
            CaseTitle = session.Case?.Title ?? string.Empty,
            QuestionText = userMessage?.Content ?? string.Empty,
            CurrentAnswerText = expertMessage.Content,
            StructuredDiagnosis = null,
            DifferentialDiagnoses = null,
            KeyImagingFindings = null,
            ReflectiveQuestions = null,
            Status = session.Status,
            EscalatedById = session.LecturerId,
            EscalatedAt = session.UpdatedAt,
            AiConfidenceScore = null,
            ClassId = enrollment?.ClassId,
            ClassName = enrollment?.Class?.ClassName ?? string.Empty,
            ReviewNote = null,
            PromotedCaseId = session.PromotedCaseId,
            Citations = new List<ExpertCitationDto>(),
            ImageUrl = ResolveSessionImageUrl(session),
            CustomCoordinates = userMessage?.Coordinates
        };
    }

    public async Task ApproveSessionAsync(Guid expertId, Guid sessionId)
    {
        var session = await _unitOfWork.Context.VisualQaSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId)
            ?? throw new KeyNotFoundException("Không tìm thấy phiên hỏi đáp.");

        var enrollment = await _unitOfWork.Context.ClassEnrollments
            .Include(e => e.Class)
            .FirstOrDefaultAsync(e =>
                e.StudentId == session.StudentId &&
                e.Class.ExpertId == expertId);

        if (enrollment == null)
            throw new InvalidOperationException("Chuyên gia không có quyền duyệt phiên hỏi đáp này.");

        session.Status = "ExpertApproved";
        session.ExpertId = expertId;
        session.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveAsync();
    }

    public async Task<Guid> PromoteToLibraryAsync(Guid expertId, Guid sessionId, PromoteToLibraryRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
            throw new InvalidOperationException("Description là bắt buộc.");
        if (string.IsNullOrWhiteSpace(request.SuggestedDiagnosis))
            throw new InvalidOperationException("SuggestedDiagnosis là bắt buộc.");
        if (string.IsNullOrWhiteSpace(request.KeyFindings))
            throw new InvalidOperationException("KeyFindings là bắt buộc.");
        if (string.IsNullOrWhiteSpace(request.ReflectiveQuestions))
            throw new InvalidOperationException("ReflectiveQuestions là bắt buộc.");

        var session = await _unitOfWork.Context.VisualQaSessions
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.Id == sessionId)
            ?? throw new KeyNotFoundException("Không tìm thấy phiên hỏi đáp.");

        var enrollment = await _unitOfWork.Context.ClassEnrollments
            .Include(e => e.Class)
            .FirstOrDefaultAsync(e =>
                e.StudentId == session.StudentId &&
                e.Class.ExpertId == expertId);

        if (enrollment == null)
            throw new InvalidOperationException("Chuyên gia không có quyền đưa phiên hỏi đáp này vào thư viện.");

        if (!string.Equals(session.Status, "ExpertApproved", StringComparison.Ordinal))
            throw new InvalidOperationException("Chỉ có thể đưa vào thư viện khi phiên đã được chuyên gia duyệt.");

        if (session.PromotedCaseId.HasValue)
            throw new InvalidOperationException("Ca này đã được đưa vào thư viện.");

        if (string.IsNullOrWhiteSpace(session.CustomImageUrl) || session.ImageId.HasValue)
            throw new InvalidOperationException("Chỉ có thể đưa ảnh tự tải lên vào thư viện.");

        var now = DateTime.UtcNow;

        await using var transaction = await _unitOfWork.Context.Database.BeginTransactionAsync();
        try
        {
            var newCase = new MedicalCase
            {
                Id = Guid.NewGuid(),
                Title = "Ca lâm sàng từ Cộng đồng",
                Description = request.Description.Trim(),
                SuggestedDiagnosis = request.SuggestedDiagnosis.Trim(),
                KeyFindings = request.KeyFindings.Trim(),
                ReflectiveQuestions = request.ReflectiveQuestions.Trim(),
                Difficulty = "Medium",
                IsApproved = true,
                IsActive = true,
                CreatedByExpertId = expertId,
                CreatedAt = now,
                UpdatedAt = now,
                IndexingStatus = DocumentIndexingStatuses.Pending,
                Version = 1
            };

            await _unitOfWork.Context.MedicalCases.AddAsync(newCase);

            var image = new MedicalImage
            {
                Id = Guid.NewGuid(),
                CaseId = newCase.Id,
                ImageUrl = session.CustomImageUrl.Trim(),
                Modality = "Other",
                CreatedAt = now
            };
            await _unitOfWork.Context.MedicalImages.AddAsync(image);

            var tag = await _unitOfWork.Context.Tags
                .FirstOrDefaultAsync(t => t.Name == "Student Q&A");
            if (tag == null)
            {
                tag = new Tag
                {
                    Id = Guid.NewGuid(),
                    Name = "Student Q&A",
                    Type = "Source",
                    CreatedAt = now,
                    UpdatedAt = now
                };
                await _unitOfWork.Context.Tags.AddAsync(tag);
            }

            var caseTag = new CaseTag
            {
                CaseId = newCase.Id,
                TagId = tag.Id,
                CreatedAt = now
            };
            await _unitOfWork.Context.CaseTags.AddAsync(caseTag);

            session.PromotedCaseId = newCase.Id;
            await _unitOfWork.SaveAsync();
            await transaction.CommitAsync();

            return newCase.Id;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<ExpertEscalatedAnswerDto> ResolveEscalatedAnswerAsync(Guid expertId, Guid sessionId, ResolveEscalatedAnswerRequestDto request)
    {
        var session = await _unitOfWork.Context.VisualQaSessions
            .Include(s => s.Student)
            .Include(s => s.Case!)
                .ThenInclude(c => c.MedicalImages)
            .Include(s => s.Image)
            .Include(s => s.Messages)
                .ThenInclude(m => m.Citations)
                    .ThenInclude(c => c.Chunk)
                        .ThenInclude(ch => ch.Doc)
            .Include(s => s.ExpertReviews)
            .FirstOrDefaultAsync(s => s.Id == sessionId)
            ?? throw new KeyNotFoundException("Không tìm thấy phiên hỏi đáp cần xử lý.");

        var enrollment = await _unitOfWork.Context.ClassEnrollments
            .Include(e => e.Class)
            .FirstOrDefaultAsync(e =>
                e.StudentId == session.StudentId &&
                e.Class.ExpertId == expertId);

        var existingReview = session.ExpertReviews.FirstOrDefault(r => r.ExpertId == expertId);

        if (string.Equals(session.Status, "ExpertApproved", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("Phiên hỏi đáp này đã được xử lý trước đó.");

        if (enrollment == null)
            throw new InvalidOperationException("Chuyên gia không có quyền xử lý phiên hỏi đáp này (sinh viên không thuộc lớp do bạn phụ trách).");

        if (!string.Equals(session.Status, "EscalatedToExpert", StringComparison.Ordinal))
            throw new ConflictException("Chỉ phiên đã được giảng viên chuyển lên chuyên gia mới được xử lý tại đây.");

        var now = DateTime.UtcNow;
        var assistantMessage = session.Messages
            .Where(m => m.Role == "Assistant")
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .FirstOrDefault();

        if (assistantMessage == null)
        {
            assistantMessage = new QAMessage
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                Role = "Assistant",
                CreatedAt = now
            };
            await _unitOfWork.Context.QaMessages.AddAsync(assistantMessage);
        }

        assistantMessage.Content = request.AnswerText;
        assistantMessage.SuggestedDiagnosis = request.StructuredDiagnosis;
        assistantMessage.DifferentialDiagnoses = SerializeJsonArray(request.DifferentialDiagnoses);
        assistantMessage.KeyImagingFindings = request.KeyImagingFindings;
        assistantMessage.ReflectiveQuestions = request.ReflectiveQuestions;
        assistantMessage.CreatedAt = now;

        session.Status = "ExpertApproved";
        session.ExpertId = expertId;
        session.UpdatedAt = now;

        if (existingReview == null)
        {
            existingReview = new ExpertReview
            {
                Id = Guid.NewGuid(),
                ExpertId = expertId,
                AnswerId = null,
                SessionId = session.Id,
                ReviewNote = request.ReviewNote,
                Action = "Approve",
                CreatedAt = now
            };
            await _unitOfWork.ExpertReviewRepository.AddAsync(existingReview);
        }
        else
        {
            existingReview.AnswerId = null;
            existingReview.SessionId = session.Id;
            existingReview.ReviewNote = request.ReviewNote;
            existingReview.Action = "Approve";
            await _unitOfWork.ExpertReviewRepository.UpdateAsync(existingReview);
        }

        await _unitOfWork.SaveAsync();

        await _notificationService.SendNotificationToUserAsync(
            session.StudentId,
            "Chuyên gia đã xử lý câu hỏi của bạn",
            "Câu trả lời đã được chuyên gia duyệt. Bạn có thể xem lại trong lịch sử câu hỏi.",
            "expert_review",
            $"/student/cases/history");

        await _ragExpertAnswerIndexingSignal.NotifyExpertApprovedForFutureIndexingAsync(session.Id);

        var orderedMessages = session.Messages
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .ToList();
        var userMessage = orderedMessages.FirstOrDefault(m => m.Role == "User");
        var latestAssistant = orderedMessages
            .Where(m => m.Role == "Assistant")
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .FirstOrDefault();

        return new ExpertEscalatedAnswerDto
        {
            AnswerId = session.Id,
            QuestionId = userMessage?.Id ?? Guid.Empty,
            StudentId = session.StudentId,
            StudentName = session.Student?.FullName ?? string.Empty,
            StudentEmail = session.Student?.Email ?? string.Empty,
            CaseId = session.CaseId,
            CaseTitle = session.Case?.Title ?? string.Empty,
            QuestionText = userMessage?.Content ?? string.Empty,
            CurrentAnswerText = latestAssistant?.Content,
            StructuredDiagnosis = latestAssistant?.SuggestedDiagnosis,
            DifferentialDiagnoses = latestAssistant?.DifferentialDiagnoses,
            KeyImagingFindings = latestAssistant?.KeyImagingFindings,
            ReflectiveQuestions = latestAssistant?.ReflectiveQuestions,
            Status = session.Status,
            EscalatedById = session.LecturerId,
            EscalatedAt = session.UpdatedAt,
            AiConfidenceScore = latestAssistant?.AiConfidenceScore,
            ClassId = enrollment?.ClassId,
            ClassName = enrollment?.Class?.ClassName ?? string.Empty,
            ReviewNote = existingReview.ReviewNote,
            PromotedCaseId = session.PromotedCaseId,
            Citations = MapCitations(latestAssistant?.Citations ?? Enumerable.Empty<Citation>()),
            ImageUrl = ResolveSessionImageUrl(session),
            CustomCoordinates = userMessage?.Coordinates
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
                c.Message != null &&
                c.Message.Session != null &&
                c.Message.Session.Status == "EscalatedToExpert" &&
                _unitOfWork.Context.ClassEnrollments.Any(e =>
                    e.StudentId == c.Message.Session.StudentId &&
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

    private static string? SerializeJsonArray(JsonElement? value)
    {
        if (value == null)
            return null;

        var el = value.Value;
        return el.ValueKind switch
        {
            JsonValueKind.Undefined or JsonValueKind.Null => null,
            JsonValueKind.String => string.IsNullOrWhiteSpace(el.GetString()) ? null : el.GetString(),
            JsonValueKind.Array =>
                JsonSerializer.Serialize(
                    el.EnumerateArray()
                        .Select(x => x.ValueKind == JsonValueKind.String ? (x.GetString() ?? string.Empty) : x.ToString())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Select(s => s.Trim())
                        .ToList()) is { Length: > 2 } joined
                    ? joined
                    : null,
            _ => el.ToString()
        };
    }

    private static string? ResolveSessionImageUrl(VisualQASession? session)
    {
        if (session == null)
            return null;
        if (!string.IsNullOrWhiteSpace(session.CustomImageUrl))
            return session.CustomImageUrl.Trim();
        if (!string.IsNullOrWhiteSpace(session.Image?.ImageUrl))
            return session.Image.ImageUrl.Trim();

        var images = session.Case?.MedicalImages;
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
