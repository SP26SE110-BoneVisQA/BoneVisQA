using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[Table("student_questions")]
[Index("CaseId", Name = "idx_student_questions_case")]
[Index("StudentId", Name = "idx_student_questions_student")]
public partial class StudentQuestion
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("student_id")]
    public Guid StudentId { get; set; }

    [Column("case_id")]
    public Guid? CaseId { get; set; }

    [Column("annotation_id")]
    public Guid? AnnotationId { get; set; }

    [Column("question_text")]
    public string QuestionText { get; set; } = null!;

    [Column("language")]
    public string? Language { get; set; }

    [Column("custom_image_url")]
    public string? CustomImageUrl { get; set; }

    [Column("custom_coordinates", TypeName = "jsonb")]
    public string? CustomCoordinates { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("AnnotationId")]
    [InverseProperty("StudentQuestions")]
    public virtual CaseAnnotation? Annotation { get; set; }

    [ForeignKey("CaseId")]
    [InverseProperty("StudentQuestions")]
    public virtual MedicalCase? Case { get; set; }

    [InverseProperty("Question")]
    public virtual ICollection<CaseAnswer> CaseAnswers { get; set; } = new List<CaseAnswer>();

    [ForeignKey("StudentId")]
    [InverseProperty("StudentQuestions")]
    public virtual User Student { get; set; } = null!;
}
