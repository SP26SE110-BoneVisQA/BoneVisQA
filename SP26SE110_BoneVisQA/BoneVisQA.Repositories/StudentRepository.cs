using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BoneVisQA.Repositories.Interfaces;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories;

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
            .FindByCondition(c => c.IsApproved && c.IsActive)
            .Include(c => c.Category)
            .Include(c => c.MedicalImages)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<MedicalCase>> GetFilteredCasesAsync(CaseFilter filter)
    {
        var query = _unitOfWork.MedicalCaseRepository
            .FindByCondition(c => c.IsApproved && c.IsActive)
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
        await _unitOfWork.CaseViewLogRepository.CreateAsync(log);
        await _unitOfWork.SaveAsync();
        return log;
    }

    public async Task<CaseAnnotation> CreateAnnotationAsync(CaseAnnotation annotation)
    {
        await _unitOfWork.CaseAnnotationRepository.CreateAsync(annotation);
        await _unitOfWork.SaveAsync();
        return annotation;
    }

    public async Task<StudentQuestion> CreateStudentQuestionAsync(StudentQuestion question)
    {
        await _unitOfWork.StudentQuestionRepository.CreateAsync(question);
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
        {
            return new List<Quiz>();
        }

        return await _unitOfWork.QuizRepository
            .FindByCondition(q => classIds.Contains(q.ClassId)
                        && (q.OpenTime == null || q.OpenTime <= utcNow)
                        && (q.CloseTime == null || q.CloseTime >= utcNow))
            .Include(q => q.QuizAttempts)
            .ToListAsync();
    }

    public async Task<Quiz?> GetQuizWithQuestionsAsync(Guid quizId)
    {
        return await _unitOfWork.QuizRepository
            .FindByCondition(q => q.Id == quizId)
            .Include(q => q.QuizQuestions)
            .FirstOrDefaultAsync();
    }

    public async Task<QuizAttempt?> GetQuizAttemptAsync(Guid studentId, Guid quizId)
    {
        return await _unitOfWork.QuizAttemptRepository
            .FindByCondition(a => a.StudentId == studentId && a.QuizId == quizId)
            .Include(a => a.StudentQuizAnswers)
            .FirstOrDefaultAsync();
    }

    public async Task<QuizAttempt> CreateQuizAttemptAsync(QuizAttempt attempt)
    {
        await _unitOfWork.QuizAttemptRepository.CreateAsync(attempt);
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
            await _unitOfWork.StudentQuizAnswerRepository.CreateAsync(answer);
        }
        await _unitOfWork.SaveAsync();
    }

    public async Task<(int totalCasesViewed, int totalQuestionsAsked, double? avgQuizScore)> GetStudentAggregateStatsAsync(Guid studentId)
    {
        var totalCasesViewed = await _unitOfWork.CaseViewLogRepository
            .FindByCondition(v => v.StudentId == studentId)
            .CountAsync();

        var totalQuestionsAsked = await _unitOfWork.StudentQuestionRepository
            .FindByCondition(q => q.StudentId == studentId)
            .CountAsync();

        var quizAttempts = await _unitOfWork.QuizAttemptRepository
            .FindByCondition(a => a.StudentId == studentId && a.Score != null)
            .ToListAsync();

        double? avgQuizScore = null;
        if (quizAttempts.Count > 0)
        {
            avgQuizScore = quizAttempts.Average(a => a.Score ?? 0);
        }

        return (totalCasesViewed, totalQuestionsAsked, avgQuizScore);
    }
}
