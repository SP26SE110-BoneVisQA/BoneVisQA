using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[Table("users")]
[Index("IsActive", Name = "idx_users_is_active")]
[Index("Email", Name = "users_email_key", IsUnique = true)]
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

    [Column("department")]
    [MaxLength(256)]
    public string? Department { get; set; }

    [Column("specialty")]
    [MaxLength(256)]
    public string? Specialty { get; set; }

    [Column("primary_bone_specialty_id")]
    public Guid? PrimaryBoneSpecialtyId { get; set; }

    [Column("date_of_birth")]
    public DateOnly? DateOfBirth { get; set; }

    [Column("phone_number")]
    [MaxLength(256)]
    public string? PhoneNumber { get; set; }

    [Column("gender")]
    [MaxLength(32)]
    public string? Gender { get; set; }

    /// <summary>Mã sinh viên theo trường (cột student_id).</summary>
    [Column("student_id")]
    [MaxLength(256)]
    public string? StudentSchoolId { get; set; }

    [Column("class_code")]
    [MaxLength(256)]
    public string? ClassCode { get; set; }

    [Column("address")]
    public string? Address { get; set; }

    [Column("bio")]
    public string? Bio { get; set; }

    [Column("emergency_contact")]
    [MaxLength(256)]
    public string? EmergencyContact { get; set; }

    [Column("medical_school")]
    [MaxLength(256)]
    public string? MedicalSchool { get; set; }

    [Column("medical_student_id")]
    [MaxLength(100)]
    public string? MedicalStudentId { get; set; }

    [Column("verification_status")]
    [MaxLength(50)]
    public string? VerificationStatus { get; set; } // "Pending", "Approved", "Rejected"

    [Column("verification_notes")]
    public string? VerificationNotes { get; set; }

    [Column("verified_at")]
    public DateTime? VerifiedAt { get; set; }

    [Column("verified_by")]
    public Guid? VerifiedBy { get; set; }

    /// <summary>Admin/user who performed medical verification (<c>users.verified_by</c> → self-FK).</summary>
    [ForeignKey("VerifiedBy")]
    [InverseProperty("UsersVerifiedByThisUser")]
    public virtual User? Verifier { get; set; }

    [ForeignKey("PrimaryBoneSpecialtyId")]
    [InverseProperty("UsersWithPrimarySpecialty")]
    public virtual BoneSpecialty? PrimaryBoneSpecialty { get; set; }

    [InverseProperty("Verifier")]
    public virtual ICollection<User> UsersVerifiedByThisUser { get; set; } = new List<User>();

    [InverseProperty("Lecturer")]
    public virtual ICollection<AcademicClass> AcademicClasses { get; set; } = new List<AcademicClass>();

    [InverseProperty("Expert")]
    public virtual ICollection<AcademicClass> ExpertAcademicClasses { get; set; } = new List<AcademicClass>();

    [InverseProperty("CreatedByExpert")]
    public virtual ICollection<MedicalCase> CreatedMedicalCases { get; set; } = new List<MedicalCase>();

    [InverseProperty("AssignedExpert")]
    public virtual ICollection<MedicalCase> AssignedMedicalCases { get; set; } = new List<MedicalCase>();

    [InverseProperty("CreatedByExpert")]
    public virtual ICollection<Quiz> CreatedQuizzes { get; set; } = new List<Quiz>();

    [InverseProperty("AssignedExpert")]
    public virtual ICollection<Quiz> AssignedQuizzes { get; set; } = new List<Quiz>();

    [InverseProperty("ReviewedBy")]
    public virtual ICollection<CaseAnswer> CaseAnswers { get; set; } = new List<CaseAnswer>();

    [InverseProperty("EscalatedBy")]
    public virtual ICollection<CaseAnswer> EscalatedCaseAnswers { get; set; } = new List<CaseAnswer>();

    [InverseProperty("Student")]
    public virtual ICollection<CaseViewLog> CaseViewLogs { get; set; } = new List<CaseViewLog>();

    [InverseProperty("Student")]
    public virtual ICollection<ClassEnrollment> ClassEnrollments { get; set; } = new List<ClassEnrollment>();

    [InverseProperty("Expert")]
    public virtual ICollection<ExpertReview> ExpertReviews { get; set; } = new List<ExpertReview>();

    [InverseProperty("Expert")]
    public virtual ICollection<ExpertSpecialty> ExpertSpecialties { get; set; } = new List<ExpertSpecialty>();

    [InverseProperty("Expert")]
    public virtual ICollection<ClassExpertAssignment> ExpertClassAssignments { get; set; } = new List<ClassExpertAssignment>();

    [InverseProperty("FlaggedByExpert")]
    public virtual ICollection<DocumentChunk> FlaggedDocumentChunks { get; set; } = new List<DocumentChunk>();

    [InverseProperty("Student")]
    public virtual ICollection<LearningStatistic> LearningStatistics { get; set; } = new List<LearningStatistic>();

    [InverseProperty("Student")]
    public virtual ICollection<QuizAttempt> QuizAttempts { get; set; } = new List<QuizAttempt>();

    [InverseProperty("Student")]
    public virtual ICollection<StudentQuestion> StudentQuestions { get; set; } = new List<StudentQuestion>();

    [InverseProperty("Student")]
    public virtual ICollection<VisualQASession> VisualQASessions { get; set; } = new List<VisualQASession>();

    [InverseProperty("User")]
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

    [InverseProperty("User")]
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
   
    [InverseProperty("GradedByUser")]
    public virtual ICollection<StudentQuizAnswer> GradedStudentQuizAnswers { get; set; } = new List<StudentQuizAnswer>();
}
