using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[Table("class_quiz_sessions")]
[Index("ClassId", "QuizId", Name = "class_quiz_sessions_class_id_quiz_id_key", IsUnique = true)]
public partial class ClassQuizSession
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("class_id")]
    public Guid ClassId { get; set; }

    [Column("quiz_id")]
    public Guid QuizId { get; set; }

    [Column("open_time")]
    public DateTime? OpenTime { get; set; }

    [Column("close_time")]
    public DateTime? CloseTime { get; set; }

    [Column("passing_score")]
    public int? PassingScore { get; set; }

    [Column("time_limit_minutes")]
    public int? TimeLimitMinutes { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("shuffle_questions")]
    public bool ShuffleQuestions { get; set; }

    /// <summary>Cho phép sinh viên làm lại quiz sau khi đã nộp (retake).</summary>
    [Column("allow_retake")]
    public bool AllowRetake { get; set; } = false;

    /// <summary>Đánh dấu lecturer đã bật retake cho attempt cụ thể — reset khi student nộp lại.</summary>
    [Column("retake_reset_at")]
    public DateTime? RetakeResetAt { get; set; }

    [ForeignKey("ClassId")]
    [InverseProperty("ClassQuizSessions")]
    public virtual AcademicClass Class { get; set; } = null!;

    [ForeignKey("QuizId")]
    [InverseProperty("ClassQuizSessions")]
    public virtual Quiz Quiz { get; set; } = null!;
}
