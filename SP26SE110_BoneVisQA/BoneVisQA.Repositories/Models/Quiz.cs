using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[Table("quizzes")]
public partial class Quiz
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("title")]
    public string Title { get; set; } = null!;

    [Column("open_time")]
    public DateTime? OpenTime { get; set; }

    [Column("close_time")]
    public DateTime? CloseTime { get; set; }

    [Column("time_limit")]
    public int? TimeLimit { get; set; }

    [Column("passing_score")]
    public int? PassingScore { get; set; }

    [Column("created_by_expert_id")]
    public Guid? CreatedByExpertId { get; set; }

    [Column("created_by_lecturer_id")]
    public Guid? CreatedByLecturerId { get; set; }

    [Column("assigned_expert_id")]
    public Guid? AssignedExpertId { get; set; }

    [Column("topic")]
    public string? Topic { get; set; }

    [Column("is_ai_generated")]
    public bool IsAiGenerated { get; set; } = false;

    [Column("difficulty")]
    public string? Difficulty { get; set; }

    [Column("classification")]
    public string? Classification { get; set; }

    [Column("is_verified_curriculum")]
    public bool IsVerifiedCurriculum { get; set; } = false;

    [Column("mode")]
    public string? Mode { get; set; } = "multiple_choice";

    [Column("bone_specialty_id")]
    public Guid? BoneSpecialtyId { get; set; }

    [Column("pathology_category_id")]
    public Guid? PathologyCategoryId { get; set; }

    [Column("teaching_points")]
    public int? TeachingPoints { get; set; } = 0;

    [Column("learning_objectives", TypeName = "jsonb")]
    public string? LearningObjectives { get; set; }

    [Column("target_student_level")]
    [MaxLength(50)]
    public string? TargetStudentLevel { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [InverseProperty("Quiz")]
    public virtual ICollection<ClassQuizSession> ClassQuizSessions { get; set; } = new List<ClassQuizSession>();

    [InverseProperty("Quiz")]
    public virtual ICollection<QuizAttempt> QuizAttempts { get; set; } = new List<QuizAttempt>();

    [InverseProperty("Quiz")]
    public virtual ICollection<QuizQuestion> QuizQuestions { get; set; } = new List<QuizQuestion>();
    [ForeignKey("CreatedByExpertId")]
    [InverseProperty("CreatedQuizzes")]
    public virtual User? CreatedByExpert { get; set; }

    [ForeignKey("CreatedByLecturerId")]
    [InverseProperty("CreatedLecturerQuizzes")]
    public virtual User? CreatedByLecturer { get; set; }

    [ForeignKey("AssignedExpertId")]
    [InverseProperty("AssignedQuizzes")]
    public virtual User? AssignedExpert { get; set; }

    [ForeignKey("BoneSpecialtyId")]
    [InverseProperty("Quizzes")]
    public virtual BoneSpecialty? BoneSpecialty { get; set; }

    [ForeignKey("PathologyCategoryId")]
    [InverseProperty("Quizzes")]
    public virtual PathologyCategory? PathologyCategory { get; set; }
}
