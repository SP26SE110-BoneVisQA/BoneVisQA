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
    public QuestionType? Type { get; set; }

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

    [Column("reference_answer")]
    public string? ReferenceAnswer { get; set; }

    [Column("max_score")]
    public int MaxScore { get; set; } = 10;

    /// <summary>
    /// Hint for the question - only shown in practice mode.
    /// </summary>
    [Column("hint")]
    public string? Hint { get; set; }

    /// <summary>
    /// Explanation of the correct answer - shown after submission.
    /// </summary>
    [Column("explanation")]
    public string? Explanation { get; set; }

    /// <summary>
    /// JSON array of correct answers for multi-select questions.
    /// Example: ["A", "C"] means options A and C are correct.
    /// </summary>
    [Column("correct_answers", TypeName = "jsonb")]
    public string? CorrectAnswers { get; set; }

    /// <summary>
    /// JSON array of accepted answers for fill-in-blank questions (case-insensitive).
    /// Example: ["x-ray", "X-ray", "xray"]
    /// </summary>
    [Column("accepted_answers", TypeName = "jsonb")]
    public string? AcceptedAnswers { get; set; }

    [ForeignKey("CaseId")]
    [InverseProperty("QuizQuestions")]
    public virtual MedicalCase? Case { get; set; }

    [ForeignKey("QuizId")]
    [InverseProperty("QuizQuestions")]
    public virtual Quiz Quiz { get; set; } = null!;

    [InverseProperty("Question")]
    public virtual ICollection<StudentQuizAnswer> StudentQuizAnswers { get; set; } = new List<StudentQuizAnswer>();
}