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

    Task<List<BoneVisQA.Repositories.Models.QuizSessionInfoDto>> GetQuizzesWithSessionForStudentAsync(Guid studentId, DateTime utcNow);

    /// <summary>
    /// Student đã đăng ký lớp có <see cref="ClassQuizSession"/> cho quiz này và đang trong cửa sổ mở (open/close).
    /// </summary>
    Task<bool> IsStudentEligibleForAssignedQuizAsync(Guid studentId, Guid quizId, DateTime utcNow);

    Task<Quiz?> GetQuizWithQuestionsAsync(Guid quizId);

    Task<QuizAttempt?> GetQuizAttemptAsync(Guid studentId, Guid quizId);

    Task<QuizAttempt?> GetQuizAttemptByIdAsync(Guid attemptId, Guid studentId);

    Task<QuizAttempt> CreateQuizAttemptAsync(QuizAttempt attempt);

    Task UpdateQuizAttemptAsync(QuizAttempt attempt);

    Task AddStudentQuizAnswersAsync(IEnumerable<StudentQuizAnswer> answers);

    Task<(int totalCasesViewed, int totalQuestionsAsked, int quizzesCompleted, int totalQuizAnswersSubmitted, double? avgQuizScore)> GetStudentAggregateStatsAsync(Guid studentId);

    Task<CaseAnswer> CreateCaseAnswerAsync(CaseAnswer answer);

    Task AddCitationsAsync(IEnumerable<Citation> citations);
}

