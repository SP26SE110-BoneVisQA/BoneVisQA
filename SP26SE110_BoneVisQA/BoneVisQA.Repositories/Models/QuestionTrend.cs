using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BoneVisQA.Repositories.Models;

[Table("question_trends")]
public class QuestionTrend
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("topic_id")]
    public Guid TopicId { get; set; }

    [Column("topic_type")]
    [MaxLength(20)]
    public string TopicType { get; set; } = "bone_specialty"; // 'bone_specialty' or 'pathology'

    [Column("question_count")]
    public int QuestionCount { get; set; } = 0;

    [Column("trend_direction")]
    [MaxLength(10)]
    public string TrendDirection { get; set; } = "stable"; // 'up', 'down', 'stable'

    [Column("change_percentage")]
    public decimal ChangePercentage { get; set; } = 0;

    [Column("period_start")]
    public DateOnly? PeriodStart { get; set; }

    [Column("period_end")]
    public DateOnly? PeriodEnd { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
