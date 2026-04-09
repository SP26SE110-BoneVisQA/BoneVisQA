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

    [Column("type")]
    public string? Type { get; set; }

    [Column("option_a")]
    public string? OptionA { get; set; }

    [Column("option_b")]
    public string? OptionB { get; set; }

    [Column("option_c")]
    public string? OptionC { get; set; }

    [Column("option_d")]
    public string? OptionD { get; set; }

    [Column("correct_answer")]
    public string? CorrectAnswer { get; set; }

    [Column("image_url")]
    public string? ImageUrl { get; set; }

    [ForeignKey("CaseId")]
    [InverseProperty("QuizQuestions")]
    public virtual MedicalCase? Case { get; set; }

    [ForeignKey("QuizId")]
    [InverseProperty("QuizQuestions")]
    public virtual Quiz Quiz { get; set; } = null!;

    [InverseProperty("Question")]
    public virtual ICollection<StudentQuizAnswer> StudentQuizAnswers { get; set; } = new List<StudentQuizAnswer>();
}