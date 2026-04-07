using System;
using System.Collections.Generic;
using BoneVisQA.Repositories.Models;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.DBContext;

public partial class BoneVisQADbContext : DbContext
{
    public BoneVisQADbContext()
    {
    }

    public BoneVisQADbContext(DbContextOptions<BoneVisQADbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AcademicClass> AcademicClasses { get; set; }

    public virtual DbSet<Announcement> Announcements { get; set; }

    public virtual DbSet<CaseAnnotation> CaseAnnotations { get; set; }

    public virtual DbSet<CaseAnswer> CaseAnswers { get; set; }
    public virtual DbSet<CaseTag> CaseTags { get; set; }
    public virtual DbSet<CaseViewLog> CaseViewLogs { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Citation> Citations { get; set; }

    public virtual DbSet<ClassCase> ClassCases { get; set; }

    public virtual DbSet<ClassEnrollment> ClassEnrollments { get; set; }

    public virtual DbSet<ClassQuizSession> ClassQuizSessions { get; set; }

    public virtual DbSet<ClassTag> ClassTags { get; set; }

    public virtual DbSet<Document> Documents { get; set; }

    public virtual DbSet<DocumentChunk> DocumentChunks { get; set; }

    public virtual DbSet<DocumentTag> DocumentTags { get; set; }

    public virtual DbSet<ExpertReview> ExpertReviews { get; set; }

    public virtual DbSet<LearningStatistic> LearningStatistics { get; set; }

    public virtual DbSet<MedicalCase> MedicalCases { get; set; }

    public virtual DbSet<MedicalImage> MedicalImages { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Quiz> Quizzes { get; set; }

    public virtual DbSet<QuizAttempt> QuizAttempts { get; set; }

    public virtual DbSet<QuizQuestion> QuizQuestions { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<StudentQuestion> StudentQuestions { get; set; }

    public virtual DbSet<StudentQuizAnswer> StudentQuizAnswers { get; set; }
    public virtual DbSet<Tag> Tags { get; set; }
    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserRole> UserRoles { get; set; }

    public virtual DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresEnum("auth", "aal_level", new[] { "aal1", "aal2", "aal3" })
            .HasPostgresEnum("auth", "code_challenge_method", new[] { "s256", "plain" })
            .HasPostgresEnum("auth", "factor_status", new[] { "unverified", "verified" })
            .HasPostgresEnum("auth", "factor_type", new[] { "totp", "webauthn", "phone" })
            .HasPostgresEnum("auth", "oauth_authorization_status", new[] { "pending", "approved", "denied", "expired" })
            .HasPostgresEnum("auth", "oauth_client_type", new[] { "public", "confidential" })
            .HasPostgresEnum("auth", "oauth_registration_type", new[] { "dynamic", "manual" })
            .HasPostgresEnum("auth", "oauth_response_type", new[] { "code" })
            .HasPostgresEnum("auth", "one_time_token_type", new[] { "confirmation_token", "reauthentication_token", "recovery_token", "email_change_token_new", "email_change_token_current", "phone_change_token" })
            .HasPostgresEnum("realtime", "action", new[] { "INSERT", "UPDATE", "DELETE", "TRUNCATE", "ERROR" })
            .HasPostgresEnum("realtime", "equality_op", new[] { "eq", "neq", "lt", "lte", "gt", "gte", "in" })
            .HasPostgresEnum("storage", "buckettype", new[] { "STANDARD", "ANALYTICS", "VECTOR" })
            .HasPostgresExtension("extensions", "pg_stat_statements")
            .HasPostgresExtension("extensions", "pgcrypto")
            .HasPostgresExtension("extensions", "uuid-ossp")
            .HasPostgresExtension("graphql", "pg_graphql")
            .HasPostgresExtension("vector")
            .HasPostgresExtension("vault", "supabase_vault");

        modelBuilder.Entity<AcademicClass>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("academic_classes_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Lecturer).WithMany(p => p.AcademicClasses)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("academic_classes_lecturer_id_fkey");

            entity.HasOne(d => d.Expert).WithMany(p => p.ExpertAcademicClasses)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("academic_classes_expert_id_fkey");
        });

        modelBuilder.Entity<Announcement>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("announcements_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Class).WithMany(p => p.Announcements).HasConstraintName("announcements_class_id_fkey");
        });

        modelBuilder.Entity<CaseAnnotation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("case_annotations_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Image).WithMany(p => p.CaseAnnotations).HasConstraintName("case_annotations_image_id_fkey");
        });

        modelBuilder.Entity<CaseAnswer>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("case_answers_pkey");
            entity.ToTable(t => t.HasCheckConstraint(
                "case_answers_status_check",
                "status = ANY (ARRAY['Pending'::text, 'Approved'::text, 'Edited'::text, 'Rejected'::text, 'Escalated'::text, 'Revised'::text])"));

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.GeneratedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Status).HasDefaultValueSql("'Pending'::text");

            entity.HasOne(d => d.Question).WithMany(p => p.CaseAnswers).HasConstraintName("case_answers_question_id_fkey");

            entity.HasOne(d => d.ReviewedBy).WithMany(p => p.CaseAnswers)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("case_answers_reviewed_by_id_fkey");

            entity.HasOne(d => d.EscalatedBy).WithMany(p => p.EscalatedCaseAnswers)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("case_answers_escalated_by_id_fkey");
        });

        modelBuilder.Entity<CaseTag>(entity =>
        {
            entity.HasKey(e => new { e.CaseId, e.TagId }).HasName("case_tags_pkey");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Case).WithMany(p => p.CaseTags).HasConstraintName("case_tags_case_id_fkey");

            entity.HasOne(d => d.Tag).WithMany(p => p.CaseTags).HasConstraintName("case_tags_tag_id_fkey");
        });

        modelBuilder.Entity<CaseViewLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("case_view_logs_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.IsCompleted).HasDefaultValue(false);
            entity.Property(e => e.ViewedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Case).WithMany(p => p.CaseViewLogs).HasConstraintName("case_view_logs_case_id_fkey");

            entity.HasOne(d => d.Student).WithMany(p => p.CaseViewLogs).HasConstraintName("case_view_logs_student_id_fkey");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("categories_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
        });

        modelBuilder.Entity<Citation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("citations_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");

            entity.HasOne(d => d.Answer).WithMany(p => p.Citations).HasConstraintName("citations_answer_id_fkey");

            entity.HasOne(d => d.Chunk).WithMany(p => p.Citations).HasConstraintName("citations_chunk_id_fkey");
        });

        modelBuilder.Entity<ClassEnrollment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("class_enrollments_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.EnrolledAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Class).WithMany(p => p.ClassEnrollments).HasConstraintName("class_enrollments_class_id_fkey");

            entity.HasOne(d => d.Student).WithMany(p => p.ClassEnrollments).HasConstraintName("class_enrollments_student_id_fkey");
        });

        modelBuilder.Entity<ClassCase>(entity =>
        {
            entity.HasKey(e => new { e.ClassId, e.CaseId }).HasName("class_cases_pkey");

            entity.Property(e => e.AssignedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsMandatory).HasDefaultValue(false);

            entity.HasOne(d => d.Class).WithMany(p => p.ClassCases)
                .HasForeignKey(d => d.ClassId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("class_cases_class_id_fkey");

            entity.HasOne(d => d.Case).WithMany(p => p.ClassCases)
                .HasForeignKey(d => d.CaseId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("class_cases_case_id_fkey");
        });

        modelBuilder.Entity<ClassTag>(entity =>
        {
            entity.HasKey(e => new { e.ClassId, e.TagId }).HasName("class_tags_pkey");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Class).WithMany(p => p.ClassTags).HasConstraintName("class_tags_class_id_fkey");

            entity.HasOne(d => d.Tag).WithMany(p => p.ClassTags).HasConstraintName("class_tags_tag_id_fkey");
        });

        modelBuilder.Entity<ClassQuizSession>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("class_quiz_sessions_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Class).WithMany(p => p.ClassQuizSessions)
                .HasForeignKey(d => d.ClassId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("class_quiz_sessions_class_id_fkey");

            entity.HasOne(d => d.Quiz).WithMany(p => p.ClassQuizSessions)
                .HasForeignKey(d => d.QuizId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("class_quiz_sessions_quiz_id_fkey");
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("documents_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsOutdated).HasDefaultValue(false);
            entity.Property(e => e.Version).HasDefaultValue(1);

            entity.HasOne(d => d.Category).WithMany(p => p.Documents)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("documents_category_id_fkey");
        });

        modelBuilder.Entity<DocumentTag>(entity =>
        {
            entity.HasKey(dt => new { dt.DocumentId, dt.TagId });

            entity.HasOne(dt => dt.Document)
                  .WithMany(d => d.DocumentTags)
                  .HasForeignKey(dt => dt.DocumentId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(dt => dt.Tag)
                  .WithMany(t => t.DocumentTags)
                  .HasForeignKey(dt => dt.TagId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("NOW()");
        });

        modelBuilder.Entity<DocumentChunk>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("document_chunks_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");

            // Ensure pgvector embedding dimension matches the embedding model output.
            entity.Property(e => e.Embedding).HasColumnType("vector(768)");
            entity.HasIndex(e => e.Embedding)
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops");

            entity.HasOne(d => d.Doc).WithMany(p => p.DocumentChunks).HasConstraintName("document_chunks_doc_id_fkey");
        });

        modelBuilder.Entity<ExpertReview>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("expert_reviews_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Answer).WithMany(p => p.ExpertReviews).HasConstraintName("expert_reviews_answer_id_fkey");

            entity.HasOne(d => d.Expert).WithMany(p => p.ExpertReviews).HasConstraintName("expert_reviews_expert_id_fkey");
        });

        modelBuilder.Entity<LearningStatistic>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("learning_statistics_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.LastUpdated).HasDefaultValueSql("now()");
            entity.Property(e => e.TotalCasesViewed).HasDefaultValue(0);
            entity.Property(e => e.TotalQuestionsAsked).HasDefaultValue(0);

            entity.HasOne(d => d.Class).WithMany(p => p.LearningStatistics)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("learning_statistics_class_id_fkey");

            entity.HasOne(d => d.Student).WithMany(p => p.LearningStatistics).HasConstraintName("learning_statistics_student_id_fkey");
        });

        modelBuilder.Entity<MedicalCase>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("medical_cases_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsApproved).HasDefaultValue(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.Category).WithMany(p => p.MedicalCases)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("medical_cases_category_id_fkey");

            entity.HasOne(d => d.CreatedByExpert).WithMany(p => p.CreatedMedicalCases)
                .HasForeignKey(d => d.CreatedByExpertId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("medical_cases_created_by_expert_id_fkey");

            entity.HasOne(d => d.AssignedExpert).WithMany(p => p.AssignedMedicalCases)
                .HasForeignKey(d => d.AssignedExpertId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("medical_cases_assigned_expert_id_fkey");
        });

        modelBuilder.Entity<MedicalImage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("medical_images_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Case).WithMany(p => p.MedicalImages).HasConstraintName("medical_images_case_id_fkey");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("notifications_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsRead).HasDefaultValue(false);

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("notifications_user_id_fkey");
        });

        //modelBuilder.Entity<Quiz>(entity =>
        //{
        //    entity.HasKey(e => e.Id).HasName("quizzes_pkey");

        //    entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
        //    entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

        //    entity.HasOne(d => d.Class).WithMany(p => p.Quizzes).HasConstraintName("quizzes_class_id_fkey");
        //});

        modelBuilder.Entity<Quiz>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("quizzes_pkey");
            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.CreatedByExpert).WithMany(p => p.CreatedQuizzes)
                .HasForeignKey(d => d.CreatedByExpertId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("quizzes_created_by_expert_id_fkey");

            entity.HasOne(d => d.AssignedExpert).WithMany(p => p.AssignedQuizzes)
                .HasForeignKey(d => d.AssignedExpertId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("quizzes_assigned_expert_id_fkey");
        });

        modelBuilder.Entity<QuizAttempt>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("quiz_attempts_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.StartedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Quiz).WithMany(p => p.QuizAttempts).HasConstraintName("quiz_attempts_quiz_id_fkey");

            entity.HasOne(d => d.Student).WithMany(p => p.QuizAttempts).HasConstraintName("quiz_attempts_student_id_fkey");
        });

        modelBuilder.Entity<QuizQuestion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("quiz_questions_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");

            entity.HasOne(d => d.Case).WithMany(p => p.QuizQuestions)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("quiz_questions_case_id_fkey");

            entity.HasOne(d => d.Quiz).WithMany(p => p.QuizQuestions).HasConstraintName("quiz_questions_quiz_id_fkey");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("roles_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<StudentQuestion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("student_questions_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Annotation).WithMany(p => p.StudentQuestions)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("student_questions_annotation_id_fkey");

            entity.HasOne(d => d.Case).WithMany(p => p.StudentQuestions).HasConstraintName("student_questions_case_id_fkey");

            entity.HasOne(d => d.Student).WithMany(p => p.StudentQuestions).HasConstraintName("student_questions_student_id_fkey");
        });

        modelBuilder.Entity<StudentQuizAnswer>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("student_quiz_answers_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");

            entity.HasOne(d => d.Attempt).WithMany(p => p.StudentQuizAnswers).HasConstraintName("student_quiz_answers_attempt_id_fkey");

            entity.HasOne(d => d.Question).WithMany(p => p.StudentQuizAnswers).HasConstraintName("student_quiz_answers_question_id_fkey");
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tags_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("user_roles_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.AssignedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Role).WithMany(p => p.UserRoles).HasConstraintName("user_roles_role_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.UserRoles).HasConstraintName("user_roles_user_id_fkey");
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("password_reset_tokens_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsUsed).HasDefaultValue(false);

            entity.HasOne(d => d.User)
                  .WithMany()
                  .HasForeignKey(d => d.UserId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("password_reset_tokens_user_id_fkey");

            entity.HasIndex(e => e.Token).IsUnique().HasDatabaseName("idx_password_reset_tokens_token");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
