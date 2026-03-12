using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[Table("student_quiz_answers")]
[Index("AttemptId", "QuestionId", Name = "student_quiz_answers_attempt_id_question_id_key", IsUnique = true)]
public partial class StudentQuizAnswer
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("attempt_id")]
    public Guid AttemptId { get; set; }

    [Column("question_id")]
    public Guid QuestionId { get; set; }

    [Column("student_answer")]
    public string? StudentAnswer { get; set; }

    [Column("is_correct")]
    public bool? IsCorrect { get; set; }

    [ForeignKey("AttemptId")]
    [InverseProperty("StudentQuizAnswers")]
    public virtual QuizAttempt Attempt { get; set; } = null!;

    [ForeignKey("QuestionId")]
    [InverseProperty("StudentQuizAnswers")]
    public virtual QuizQuestion Question { get; set; } = null!;
}
