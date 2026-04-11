using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BoneVisQA.Repositories.Interfaces;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Services;

public class StudentRepository : IStudentRepository
{
    private readonly IUnitOfWork _unitOfWork;

    public StudentRepository(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<MedicalCase>> GetAllCasesAsync()
    {
        return await _unitOfWork.MedicalCaseRepository
            .FindByCondition(c => c.IsApproved == true && c.IsActive == true)
            .Include(c => c.Category)
            .Include(c => c.MedicalImages)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<MedicalCase>> GetFilteredCasesAsync(CaseFilter filter)
    {
        var query = _unitOfWork.MedicalCaseRepository
            .FindByCondition(c => c.IsApproved == true && c.IsActive == true)
            .Include(c => c.Category)
            .Include(c => c.CaseTags)
                .ThenInclude(ct => ct.Tag)
            .Include(c => c.MedicalImages)
            .AsQueryable();

        if (filter.CategoryId.HasValue)
        {
            query = query.Where(c => c.CategoryId == filter.CategoryId);
        }

        if (!string.IsNullOrEmpty(filter.Difficulty))
        {
            query = query.Where(c => c.Difficulty == filter.Difficulty);
        }

        if (!string.IsNullOrEmpty(filter.Location) || !string.IsNullOrEmpty(filter.LessonType))
        {
            var tagTypes = new List<string>();
            if (!string.IsNullOrEmpty(filter.Location)) tagTypes.Add(filter.Location);
            if (!string.IsNullOrEmpty(filter.LessonType)) tagTypes.Add(filter.LessonType);

            query = query.Where(c => c.CaseTags.Any(ct => tagTypes.Contains(ct.Tag.Type)));
        }

        return await query.OrderByDescending(c => c.CreatedAt).ToListAsync();
    }

    public async Task<MedicalCase?> GetCaseWithImagesAsync(Guid caseId)
    {
        return await _unitOfWork.MedicalCaseRepository
            .FindByCondition(c => c.Id == caseId)
            .Include(c => c.Category)
            .Include(c => c.CaseTags)
                .ThenInclude(ct => ct.Tag)
            .Include(c => c.MedicalImages)
            .FirstOrDefaultAsync();
    }

    public async Task<CaseViewLog> AddCaseViewLogAsync(CaseViewLog log)
    {
        await _unitOfWork.CaseViewLogRepository.AddAsync(log);
        await _unitOfWork.SaveAsync();
        return log;
    }

    public async Task<CaseAnnotation> CreateAnnotationAsync(CaseAnnotation annotation)
    {
        await _unitOfWork.CaseAnnotationRepository.AddAsync(annotation);
        await _unitOfWork.SaveAsync();
        return annotation;
    }

    public async Task<StudentQuestion> CreateStudentQuestionAsync(StudentQuestion question)
    {
        await _unitOfWork.StudentQuestionRepository.AddAsync(question);
        await _unitOfWork.SaveAsync();
        return question;
    }

    public async Task<List<StudentQuestion>> GetQuestionsByStudentAsync(Guid studentId)
    {
        return await _unitOfWork.StudentQuestionRepository
            .FindByCondition(q => q.StudentId == studentId)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Announcement>> GetAnnouncementsForStudentAsync(Guid studentId)
    {
        var classIds = await _unitOfWork.ClassEnrollmentRepository
            .FindByCondition(e => e.StudentId == studentId)
            .Select(e => e.ClassId)
            .ToListAsync();

        if (classIds.Count == 0)
        {
            return new List<Announcement>();
        }

        return await _unitOfWork.AnnouncementRepository
            .FindByCondition(a => classIds.Contains(a.ClassId))
            .Include(a => a.Class)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Quiz>> GetQuizzesForStudentAsync(Guid studentId, DateTime utcNow)
    {
        var classIds = await _unitOfWork.ClassEnrollmentRepository
            .FindByCondition(e => e.StudentId == studentId)
            .Select(e => e.ClassId)
            .ToListAsync();

        if (classIds.Count == 0)
            return new List<Quiz>();

        var quizIds = await _unitOfWork.Context.ClassQuizSessions
            .Include(cqs => cqs.Quiz)
            .Where(cqs => classIds.Contains(cqs.ClassId))
            .Where(cqs =>
                ((cqs.OpenTime ?? cqs.Quiz!.OpenTime) == null || (cqs.OpenTime ?? cqs.Quiz!.OpenTime) <= utcNow)
                && ((cqs.CloseTime ?? cqs.Quiz!.CloseTime) == null
                    || (cqs.CloseTime ?? cqs.Quiz!.CloseTime) >= utcNow))
            .Select(cqs => cqs.QuizId)
            .Distinct()
            .ToListAsync();

        if (quizIds.Count == 0)
            return new List<Quiz>();

        return await _unitOfWork.Context.Quizzes
            .Where(q => quizIds.Contains(q.Id))
            .Include(q => q.QuizAttempts)
            .ToListAsync();
    }

    public async Task<List<BoneVisQA.Repositories.Models.QuizSessionInfoDto>> GetQuizzesWithSessionForStudentAsync(Guid studentId, DateTime utcNow)
    {
        var classIds = await _unitOfWork.ClassEnrollmentRepository
            .FindByCondition(e => e.StudentId == studentId)
            .Select(e => e.ClassId)
            .ToListAsync();

        if (classIds.Count == 0)
            return new List<BoneVisQA.Repositories.Models.QuizSessionInfoDto>();

        return await _unitOfWork.Context.ClassQuizSessions
            .AsNoTracking()
            .Include(cqs => cqs.Quiz)
            .Include(cqs => cqs.Class)
            .Where(cqs => classIds.Contains(cqs.ClassId))
            .Where(cqs =>
                ((cqs.OpenTime ?? cqs.Quiz!.OpenTime) == null || (cqs.OpenTime ?? cqs.Quiz!.OpenTime) <= utcNow)
                && ((cqs.CloseTime ?? cqs.Quiz!.CloseTime) == null
                    || (cqs.CloseTime ?? cqs.Quiz!.CloseTime) > utcNow))
            .Select(cqs => new BoneVisQA.Repositories.Models.QuizSessionInfoDto
            {
                QuizId = cqs.QuizId,
                Title = cqs.Quiz != null ? cqs.Quiz.Title : string.Empty,
                ClassId = cqs.ClassId,
                ClassName = cqs.Class != null ? cqs.Class.ClassName : string.Empty,
                OpenTime = cqs.OpenTime,
                CloseTime = cqs.CloseTime,
                // Session có thể null: fallback sang cấu hình trên bản ghi quiz (giống lecturer UI).
                TimeLimitMinutes = cqs.TimeLimitMinutes ?? (cqs.Quiz != null ? cqs.Quiz.TimeLimit : null),
                PassingScore = cqs.PassingScore ?? (cqs.Quiz != null ? cqs.Quiz.PassingScore : null)
            })
            .ToListAsync();
    }

    public async Task<bool> IsStudentEligibleForAssignedQuizAsync(Guid studentId, Guid quizId, DateTime utcNow)
    {
        var classIds = await _unitOfWork.ClassEnrollmentRepository
            .FindByCondition(e => e.StudentId == studentId)
            .Select(e => e.ClassId)
            .ToListAsync();

        if (classIds.Count == 0)
            return false;

        return await _unitOfWork.Context.ClassQuizSessions
            .Include(cqs => cqs.Quiz)
            .AnyAsync(cqs => cqs.QuizId == quizId
                && classIds.Contains(cqs.ClassId)
                && ((cqs.OpenTime ?? cqs.Quiz!.OpenTime) == null
                    || (cqs.OpenTime ?? cqs.Quiz!.OpenTime) <= utcNow)
                && ((cqs.CloseTime ?? cqs.Quiz!.CloseTime) == null
                    || (cqs.CloseTime ?? cqs.Quiz!.CloseTime) > utcNow));
    }

    public async Task<Quiz?> GetQuizWithQuestionsAsync(Guid quizId)
    {
        return await _unitOfWork.QuizRepository
            .FindByCondition(q => q.Id == quizId)
            .Include(q => q.QuizQuestions)
                .ThenInclude(qq => qq.Case)
                    .ThenInclude(c => c!.MedicalImages)
            .FirstOrDefaultAsync();
    }

    public async Task<QuizAttempt?> GetQuizAttemptAsync(Guid studentId, Guid quizId)
    {
        return await _unitOfWork.QuizAttemptRepository
            .FindByCondition(a => a.StudentId == studentId && a.QuizId == quizId)
            .Include(a => a.StudentQuizAnswers)
            .FirstOrDefaultAsync();
    }

    public async Task<QuizAttempt?> GetQuizAttemptByIdAsync(Guid attemptId, Guid studentId)
    {
        return await _unitOfWork.QuizAttemptRepository
            .FindByCondition(a => a.Id == attemptId && a.StudentId == studentId)
            .Include(a => a.StudentQuizAnswers)
            .FirstOrDefaultAsync();
    }

    public async Task<QuizAttempt> CreateQuizAttemptAsync(QuizAttempt attempt)
    {
        await _unitOfWork.QuizAttemptRepository.AddAsync(attempt);
        await _unitOfWork.SaveAsync();
        return attempt;
    }

    public async Task UpdateQuizAttemptAsync(QuizAttempt attempt)
    {
        await _unitOfWork.QuizAttemptRepository.UpdateAsync(attempt);
        await _unitOfWork.SaveAsync();
    }

    public async Task AddStudentQuizAnswersAsync(IEnumerable<StudentQuizAnswer> answers)
    {
        foreach (var answer in answers)
        {
            await _unitOfWork.StudentQuizAnswerRepository.AddAsync(answer);
        }
    }

    public async Task<(int totalCasesViewed, int totalQuestionsAsked, int quizzesCompleted, int totalQuizAnswersSubmitted, double? avgQuizScore)> GetStudentAggregateStatsAsync(Guid studentId)
    {
        var totalCasesViewed = await _unitOfWork.CaseViewLogRepository
            .FindByCondition(v => v.StudentId == studentId)
            .CountAsync();

        var totalQuestionsAsked = await _unitOfWork.StudentQuestionRepository
            .FindByCondition(q => q.StudentId == studentId)
            .CountAsync();

        var quizzesCompleted = await _unitOfWork.QuizAttemptRepository
            .FindByCondition(a => a.StudentId == studentId && (a.CompletedAt != null || a.Score != null))
            .CountAsync();

        var totalQuizAnswersSubmitted = await _unitOfWork.StudentQuizAnswerRepository
            .FindByCondition(a => a.Attempt.StudentId == studentId)
            .CountAsync();

        var quizAttempts = await _unitOfWork.QuizAttemptRepository
            .FindByCondition(a => a.StudentId == studentId && a.Score != null)
            .ToListAsync();

        double? avgQuizScore = null;
        if (quizAttempts.Count > 0)
        {
            avgQuizScore = quizAttempts.Average(a => a.Score ?? 0);
        }

        return (totalCasesViewed, totalQuestionsAsked, quizzesCompleted, totalQuizAnswersSubmitted, avgQuizScore);
    }

    public async Task<CaseAnswer> CreateCaseAnswerAsync(CaseAnswer answer)
    {
        await _unitOfWork.CaseAnswerRepository.AddAsync(answer);
        await _unitOfWork.SaveAsync();
        return answer;
    }

    public async Task AddCitationsAsync(IEnumerable<Citation> citations)
    {
        foreach (var citation in citations)
        {
            await _unitOfWork.CitationRepository.AddAsync(citation);
        }
        await _unitOfWork.SaveAsync();
    }
}
