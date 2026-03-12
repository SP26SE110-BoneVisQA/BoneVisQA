using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[Table("class_quizzes")]
public partial class ClassQuiz
{
    [Column("class_id")]
    public Guid ClassId { get; set; }

    [Column("quiz_id")]
    public Guid QuizId { get; set; }

    [Column("assigned_at")]
    public DateTime? AssignedAt { get; set; }

    [ForeignKey("ClassId")]
    [InverseProperty("ClassQuizzes")]
    public virtual AcademicClass Class { get; set; } = null!;

    [ForeignKey("QuizId")]
    [InverseProperty("ClassQuizzes")]
    public virtual Quiz Quiz { get; set; } = null!;
}
