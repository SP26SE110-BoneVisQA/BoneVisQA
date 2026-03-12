using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BoneVisQA.Repositories.Models;

namespace BoneVisQA.Repositories.Interfaces;

public interface IStudentRepository
{
    Task<List<MedicalCase>> GetAllCasesAsync();

    Task<List<MedicalCase>> GetFilteredCasesAsync(CaseFilter filter);

    Task<MedicalCase?> GetCaseWithImagesAsync(Guid caseId);

    Task<CaseViewLog> AddCaseViewLogAsync(CaseViewLog log);

    Task<CaseAnnotation> CreateAnnotationAsync(CaseAnnotation annotation);

    Task<StudentQuestion> CreateStudentQuestionAsync(StudentQuestion question);

    Task<List<StudentQuestion>> GetQuestionsByStudentAsync(Guid studentId);

    Task<List<Announcement>> GetAnnouncementsForStudentAsync(Guid studentId);

    Task<List<Quiz>> GetQuizzesForStudentAsync(Guid studentId, DateTime utcNow);

    Task<Quiz?> GetQuizWithQuestionsAsync(Guid quizId);

    Task<QuizAttempt?> GetQuizAttemptAsync(Guid studentId, Guid quizId);

    Task<QuizAttempt?> GetQuizAttemptByIdAsync(Guid attemptId, Guid studentId);

    Task<QuizAttempt> CreateQuizAttemptAsync(QuizAttempt attempt);

    Task UpdateQuizAttemptAsync(QuizAttempt attempt);

    Task AddStudentQuizAnswersAsync(IEnumerable<StudentQuizAnswer> answers);

    Task<(int totalCasesViewed, int totalQuestionsAsked, double? avgQuizScore)> GetStudentAggregateStatsAsync(Guid studentId);
}

