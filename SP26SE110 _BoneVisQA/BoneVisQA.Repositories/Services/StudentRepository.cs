using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BoneVisQA.Repositories.DBContext;
using BoneVisQA.Repositories.Interfaces;
using BoneVisQA.Repositories.Models;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Services;

public class StudentRepository : IStudentRepository
{
    private readonly BoneVisQADbContext _context;

    public StudentRepository(BoneVisQADbContext context)
    {
        _context = context;
    }

    public async Task<List<MedicalCase>> GetAllCasesAsync()
    {
        return await _context.MedicalCases
            .AsNoTracking()
            .Include(c => c.Category)
            .Include(c => c.MedicalImages)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<MedicalCase?> GetCaseWithImagesAsync(Guid caseId)
    {
        return await _context.MedicalCases
            .AsNoTracking()
            .Include(c => c.Category)
            .Include(c => c.MedicalImages)
            .FirstOrDefaultAsync(c => c.Id == caseId);
    }

    public async Task<CaseViewLog> AddCaseViewLogAsync(CaseViewLog log)
    {
        _context.CaseViewLogs.Add(log);
        await _context.SaveChangesAsync();
        return log;
    }

    public async Task<CaseAnnotation> CreateAnnotationAsync(CaseAnnotation annotation)
    {
        _context.CaseAnnotations.Add(annotation);
        await _context.SaveChangesAsync();
        return annotation;
    }

    public async Task<StudentQuestion> CreateStudentQuestionAsync(StudentQuestion question)
    {
        _context.StudentQuestions.Add(question);
        await _context.SaveChangesAsync();
        return question;
    }

    public async Task<List<StudentQuestion>> GetQuestionsByStudentAsync(Guid studentId)
    {
        return await _context.StudentQuestions
            .AsNoTracking()
            .Where(q => q.StudentId == studentId)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();
    }

    //public async Task<List<Quiz>> GetQuizzesForStudentAsync(Guid studentId, DateTime utcNow)
    //{
    //    var classIds = await _context.ClassEnrollments
    //        .AsNoTracking()
    //        .Where(e => e.StudentId == studentId)
    //        .Select(e => e.ClassId)
    //        .ToListAsync();

    //    if (classIds.Count == 0)
    //    {
    //        return new List<Quiz>();
    //    }

    //    return await _context.Quizzes
    //        .AsNoTracking()
    //        .Where(q => classIds.Contains(q.ClassId)
    //                    && (q.OpenTime == null || q.OpenTime <= utcNow)
    //                    && (q.CloseTime == null || q.CloseTime >= utcNow))
    //        .Include(q => q.QuizAttempts)
    //        .ToListAsync();
    //}

    public async Task<Quiz?> GetQuizWithQuestionsAsync(Guid quizId)
    {
        return await _context.Quizzes
            .AsNoTracking()
            .Include(q => q.QuizQuestions)
            .FirstOrDefaultAsync(q => q.Id == quizId);
    }

    public async Task<QuizAttempt?> GetQuizAttemptAsync(Guid studentId, Guid quizId)
    {
        return await _context.QuizAttempts
            .Include(a => a.StudentQuizAnswers)
            .FirstOrDefaultAsync(a => a.StudentId == studentId && a.QuizId == quizId);
    }

    public async Task<QuizAttempt> CreateQuizAttemptAsync(QuizAttempt attempt)
    {
        _context.QuizAttempts.Add(attempt);
        await _context.SaveChangesAsync();
        return attempt;
    }

    public async Task UpdateQuizAttemptAsync(QuizAttempt attempt)
    {
        _context.QuizAttempts.Update(attempt);
        await _context.SaveChangesAsync();
    }

    public async Task AddStudentQuizAnswersAsync(IEnumerable<StudentQuizAnswer> answers)
    {
        _context.StudentQuizAnswers.AddRange(answers);
        await _context.SaveChangesAsync();
    }

    public async Task<(int totalCasesViewed, int totalQuestionsAsked, double? avgQuizScore)> GetStudentAggregateStatsAsync(Guid studentId)
    {
        var totalCasesViewed = await _context.CaseViewLogs
            .AsNoTracking()
            .Where(v => v.StudentId == studentId)
            .CountAsync();

        var totalQuestionsAsked = await _context.StudentQuestions
            .AsNoTracking()
            .Where(q => q.StudentId == studentId)
            .CountAsync();

        var quizAttempts = await _context.QuizAttempts
            .AsNoTracking()
            .Where(a => a.StudentId == studentId && a.Score != null)
            .ToListAsync();

        double? avgQuizScore = null;
        if (quizAttempts.Count > 0)
        {
            avgQuizScore = quizAttempts.Average(a => a.Score ?? 0);
        }

        return (totalCasesViewed, totalQuestionsAsked, avgQuizScore);
    }
}

