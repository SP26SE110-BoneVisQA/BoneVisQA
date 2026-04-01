using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[Table("class_quizzes")]
[PrimaryKey(nameof(ClassId), nameof(QuizId))]
[Index("ClassId")]
[Index("QuizId")]
public partial class ClassQuiz
{
    [Column("class_id")]
    public Guid ClassId { get; set; }

    [Column("quiz_id")]
    public Guid QuizId { get; set; }

    [Column("assigned_at")]
    public DateTime? AssignedAt { get; set; }

    [ForeignKey(nameof(ClassId))]
    [InverseProperty(nameof(AcademicClass.ClassQuizzes))]
    public virtual AcademicClass Class { get; set; } = null!;

    [ForeignKey(nameof(QuizId))]
    [InverseProperty(nameof(Quiz.ClassQuizzes))]
    public virtual Quiz Quiz { get; set; } = null!;
}
