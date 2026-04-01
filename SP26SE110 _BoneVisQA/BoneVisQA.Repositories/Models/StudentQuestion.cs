using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[Table("student_questions")]
[Index("StudentId", Name = "idx_student_questions_student")]
public partial class StudentQuestion
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("student_id")]
    public Guid StudentId { get; set; }

    [Column("case_id")]
    public Guid CaseId { get; set; }

    [Column("annotation_id")]
    public Guid? AnnotationId { get; set; }

    [Column("question_text")]
    public string QuestionText { get; set; } = null!;

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("AnnotationId")]
    [InverseProperty("StudentQuestions")]
    public virtual CaseAnnotation? Annotation { get; set; }

    [ForeignKey("CaseId")]
    [InverseProperty("StudentQuestions")]
    public virtual MedicalCase Case { get; set; } = null!;

    [InverseProperty("Question")]
    public virtual ICollection<CaseAnswer> CaseAnswers { get; set; } = new List<CaseAnswer>();

    [ForeignKey("StudentId")]
    [InverseProperty("StudentQuestions")]
    public virtual UserProfile Student { get; set; } = null!;
}
