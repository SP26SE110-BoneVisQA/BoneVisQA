using BoneVisQA.Repositories.Basic;
using BoneVisQA.Repositories.DBContext;
using BoneVisQA.Repositories.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Repositories.UnitOfWork
{
    public interface IUnitOfWork : IDisposable
    {
        BoneVisQADbContext Context { get; }
        GenericRepository<AcademicClass> AcademicClassRepository { get; }
        GenericRepository<Announcement> AnnouncementRepository { get; }
        GenericRepository<CaseAnnotation> CaseAnnotationRepository { get; }
        GenericRepository<CaseAnswer> CaseAnswerRepository { get; }
        GenericRepository<CaseViewLog> CaseViewLogRepository { get; }
        GenericRepository<Category> CategoryRepository { get; }
        GenericRepository<Citation> CitationRepository { get; }
        GenericRepository<ClassCase> ClassCaseRepository { get; }
        GenericRepository<ClassEnrollment> ClassEnrollmentRepository { get; }
        GenericRepository<ClassQuiz> ClassQuizRepository { get; }
        GenericRepository<ClassQuizSession> ClassQuizSessionRepository { get; }
        GenericRepository<Document> DocumentRepository { get; }
        GenericRepository<DocumentChunk> DocumentChunkRepository { get; }
        GenericRepository<ExpertReview> ExpertReviewRepository { get; }
        GenericRepository<LearningStatistic> LearningStatisticRepository { get; }
        GenericRepository<MedicalCase> MedicalCaseRepository { get; }
        GenericRepository<MedicalImage> MedicalImageRepository { get; }
        GenericRepository<Quiz> QuizRepository { get; }
        GenericRepository<QuizAttempt> QuizAttemptRepository { get; }
        GenericRepository<QuizQuestion> QuizQuestionRepository { get; }
        GenericRepository<StudentQuestion> StudentQuestionRepository { get; }
        GenericRepository<StudentQuizAnswer> StudentQuizAnswerRepository { get; }
        GenericRepository<User> UserRepository { get; }
        GenericRepository<Role> RoleRepository { get; }
        GenericRepository<UserRole> UserRoleRepository { get; }
        GenericRepository<CaseTag> CaseTagRepository { get; }
        GenericRepository<Tag> TagRepository { get; }
        GenericRepository<DocumentTag> DocumentTagRepository { get; }
        GenericRepository<PasswordResetToken> PasswordResetTokenRepository { get; }
        int Save();
        Task<int> SaveAsync();
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();

    }
}
