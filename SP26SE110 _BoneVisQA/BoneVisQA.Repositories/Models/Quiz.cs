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

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    //[ForeignKey("ClassId")]
    //[InverseProperty("Quizzes")]
    //public virtual AcademicClass Class { get; set; } = null!;

    [InverseProperty("Quiz")]
    public virtual ICollection<QuizAttempt> QuizAttempts { get; set; } = new List<QuizAttempt>();

    [InverseProperty("Quiz")]
    public virtual ICollection<QuizQuestion> QuizQuestions { get; set; } = new List<QuizQuestion>();
    public ICollection<ClassQuiz> ClassQuizzes { get; set; } = new List<ClassQuiz>();

}
