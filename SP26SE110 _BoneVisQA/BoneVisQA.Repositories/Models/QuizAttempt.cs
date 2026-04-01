using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[Table("quiz_attempts")]
[Index("StudentId", "QuizId", Name = "quiz_attempts_student_id_quiz_id_key", IsUnique = true)]
public partial class QuizAttempt
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("student_id")]
    public Guid StudentId { get; set; }

    [Column("quiz_id")]
    public Guid QuizId { get; set; }

    [Column("score")]
    public double? Score { get; set; }

    [Column("started_at")]
    public DateTime? StartedAt { get; set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [ForeignKey("QuizId")]
    [InverseProperty("QuizAttempts")]
    public virtual Quiz Quiz { get; set; } = null!;

    [ForeignKey("StudentId")]
    [InverseProperty("QuizAttempts")]
    public virtual UserProfile Student { get; set; } = null!;

    [InverseProperty("Attempt")]
    public virtual ICollection<StudentQuizAnswer> StudentQuizAnswers { get; set; } = new List<StudentQuizAnswer>();
}
