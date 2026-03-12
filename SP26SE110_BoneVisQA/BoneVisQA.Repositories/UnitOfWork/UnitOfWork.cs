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
    public class UnitOfWork : IUnitOfWork
    {
        private readonly BoneVisQADbContext _context;

        private GenericRepository<AcademicClass> _academicclassRepository = null!;
        private GenericRepository<Announcement> _announcementRepository = null!;
        private GenericRepository<CaseAnnotation> _caseannotationRepository = null!;
        private GenericRepository<CaseAnswer> _caseanswerRepository = null!;
        private GenericRepository<CaseViewLog> _caseviewlogRepository = null!;
        private GenericRepository<Category> _categoryRepository = null!;
        private GenericRepository<Citation> _citationRepository = null!;
        private GenericRepository<ClassEnrollment> _classenrollmentRepository = null!;
        private GenericRepository<Document> _documentRepository = null!;
        private GenericRepository<DocumentChunk> _documentchunkRepository = null!;
        private GenericRepository<DocumentTag> _documenttagRepository = null!;
        private GenericRepository<ExpertReview> _expertreviewRepository = null!;
        private GenericRepository<LearningStatistic> _learningstatisticRepository = null!;
        private GenericRepository<MedicalCase> _medicalcaseRepository = null!;
        private GenericRepository<MedicalImage> _medicalimageRepository = null!;
        private GenericRepository<Quiz> _quizRepository = null!;
        private GenericRepository<QuizAttempt> _quizattemptRepository = null!;
        private GenericRepository<QuizQuestion> _quizquestionRepository = null!;
        private GenericRepository<StudentQuestion> _studentquestionRepository = null!;
        private GenericRepository<StudentQuizAnswer> _studentquizanswerRepository = null!;
        private GenericRepository<User> _userRepository = null!;
        private GenericRepository<Role> _roleRepository = null!;
        private GenericRepository<UserRole> _userroleRepository = null!;
        private GenericRepository<CaseTag> _casetagRepository = null!;
        private GenericRepository<Tag> _tagRepository = null!;

        public UnitOfWork(BoneVisQADbContext context)
        {
            _context = context;
        }

        public GenericRepository<AcademicClass> AcademicClassRepository => _academicclassRepository ??= new GenericRepository<AcademicClass>(_context);
        public GenericRepository<Announcement> AnnouncementRepository => _announcementRepository ??= new GenericRepository<Announcement>(_context);
        public GenericRepository<CaseAnnotation> CaseAnnotationRepository => _caseannotationRepository ??= new GenericRepository<CaseAnnotation>(_context);
        public GenericRepository<CaseAnswer> CaseAnswerRepository => _caseanswerRepository ??= new GenericRepository<CaseAnswer>(_context);
        public GenericRepository<CaseViewLog> CaseViewLogRepository => _caseviewlogRepository ??= new GenericRepository<CaseViewLog>(_context);
        public GenericRepository<Category> CategoryRepository => _categoryRepository ??= new GenericRepository<Category>(_context);
        public GenericRepository<Citation> CitationRepository => _citationRepository ??= new GenericRepository<Citation>(_context);
        public GenericRepository<ClassEnrollment> ClassEnrollmentRepository => _classenrollmentRepository ??= new GenericRepository<ClassEnrollment>(_context);
        public GenericRepository<Document> DocumentRepository => _documentRepository ??= new GenericRepository<Document>(_context);
        public GenericRepository<DocumentChunk> DocumentChunkRepository => _documentchunkRepository ??= new GenericRepository<DocumentChunk>(_context);
        public GenericRepository<ExpertReview> ExpertReviewRepository => _expertreviewRepository ??= new GenericRepository<ExpertReview>(_context);
        public GenericRepository<LearningStatistic> LearningStatisticRepository => _learningstatisticRepository ??= new GenericRepository<LearningStatistic>(_context);
        public GenericRepository<MedicalCase> MedicalCaseRepository => _medicalcaseRepository ??= new GenericRepository<MedicalCase>(_context);
        public GenericRepository<MedicalImage> MedicalImageRepository => _medicalimageRepository ??= new GenericRepository<MedicalImage>(_context);
        public GenericRepository<Quiz> QuizRepository => _quizRepository ??= new GenericRepository<Quiz>(_context);
        public GenericRepository<QuizAttempt> QuizAttemptRepository => _quizattemptRepository ??= new GenericRepository<QuizAttempt>(_context);
        public GenericRepository<QuizQuestion> QuizQuestionRepository => _quizquestionRepository ??= new GenericRepository<QuizQuestion>(_context);
        public GenericRepository<StudentQuestion> StudentQuestionRepository => _studentquestionRepository ??= new GenericRepository<StudentQuestion>(_context);
        public GenericRepository<StudentQuizAnswer> StudentQuizAnswerRepository => _studentquizanswerRepository ??= new GenericRepository<StudentQuizAnswer>(_context);
        public GenericRepository<User> UserRepository => _userRepository ??= new GenericRepository<User>(_context);
        public GenericRepository<Role> RoleRepository => _roleRepository ??= new GenericRepository<Role>(_context);
        public GenericRepository<UserRole> UserRoleRepository => _userroleRepository ??= new GenericRepository<UserRole>(_context);
        public GenericRepository<CaseTag> CaseTagRepository => _casetagRepository ??= new GenericRepository<CaseTag>(_context);
        public GenericRepository<Tag> TagRepository => _tagRepository ??= new GenericRepository<Tag>(_context);
        public GenericRepository<DocumentTag> DocumentTagRepository => _documenttagRepository ??= new GenericRepository<DocumentTag>(_context);

        public int Save() => _context.SaveChanges();

        public async Task<int> SaveAsync() => await _context.SaveChangesAsync();
        public async Task BeginTransactionAsync()
        {
            await _context.Database.BeginTransactionAsync();
        }

        public async Task CommitTransactionAsync()
        {
            await _context.Database.CommitTransactionAsync();
        }

        public async Task RollbackTransactionAsync()
        {
            await _context.Database.RollbackTransactionAsync();
        }

        // IDisposable
        private bool disposed = false;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    _context.Dispose();
                }
                disposed = true;
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}