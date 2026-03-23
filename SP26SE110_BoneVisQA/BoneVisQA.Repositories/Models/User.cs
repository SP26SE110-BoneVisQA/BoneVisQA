using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[Table("users")]
[Index("IsActive", Name = "idx_users_is_active")]
[Index("Email", Name = "users_email_key", IsUnique = true)]
[Index("IsActive", Name = "idx_users_is_active")]
public partial class User
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("full_name")]
    public string FullName { get; set; } = null!;

    [Column("email")]
    public string Email { get; set; } = null!;

    [Column("password")]
    public string? Password { get; set; }

    [Column("school_cohort")]
    public string? SchoolCohort { get; set; }

    [Column("last_login")]
    public DateTime? LastLogin { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("google_id")]
    [MaxLength(256)]
    public string? GoogleId { get; set; }

    [Column("avatar_url")]
    [MaxLength(1024)]
    public string? AvatarUrl { get; set; }

    [InverseProperty("Lecturer")]
    public virtual ICollection<AcademicClass> AcademicClasses { get; set; } = new List<AcademicClass>();

    [InverseProperty("ReviewedBy")]
    public virtual ICollection<CaseAnswer> CaseAnswers { get; set; } = new List<CaseAnswer>();

    [InverseProperty("Student")]
    public virtual ICollection<CaseViewLog> CaseViewLogs { get; set; } = new List<CaseViewLog>();

    [InverseProperty("Student")]
    public virtual ICollection<ClassEnrollment> ClassEnrollments { get; set; } = new List<ClassEnrollment>();

    [InverseProperty("Expert")]
    public virtual ICollection<ExpertReview> ExpertReviews { get; set; } = new List<ExpertReview>();

    [InverseProperty("Student")]
    public virtual ICollection<LearningStatistic> LearningStatistics { get; set; } = new List<LearningStatistic>();

    [InverseProperty("Student")]
    public virtual ICollection<QuizAttempt> QuizAttempts { get; set; } = new List<QuizAttempt>();

    [InverseProperty("Student")]
    public virtual ICollection<StudentQuestion> StudentQuestions { get; set; } = new List<StudentQuestion>();

    [InverseProperty("User")]
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
