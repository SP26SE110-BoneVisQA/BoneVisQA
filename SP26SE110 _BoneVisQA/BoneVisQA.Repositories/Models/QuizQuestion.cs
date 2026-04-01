using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[Table("quiz_questions")]
public partial class QuizQuestion
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("quiz_id")]
    public Guid QuizId { get; set; }

    [Column("case_id")]
    public Guid? CaseId { get; set; }

    [Column("question_text")]
    public string QuestionText { get; set; } = null!;

    [Column("correct_answer")]
    public string? CorrectAnswer { get; set; }

    [ForeignKey("CaseId")]
    [InverseProperty("QuizQuestions")]
    public virtual MedicalCase? Case { get; set; }

    [ForeignKey("QuizId")]
    [InverseProperty("QuizQuestions")]
    public virtual Quiz Quiz { get; set; } = null!;

    [InverseProperty("Question")]
    public virtual ICollection<StudentQuizAnswer> StudentQuizAnswers { get; set; } = new List<StudentQuizAnswer>();
}
