using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BoneVisQA.Repositories.Models;

[Table("error_patterns")]
public class ErrorPattern
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("student_id")]
    public Guid StudentId { get; set; }

    [Column("question_pattern")]
    public string? QuestionPattern { get; set; }

    [Column("error_topic")]
    [MaxLength(256)]
    public string? ErrorTopic { get; set; }

    [Column("error_count")]
    public int ErrorCount { get; set; } = 1;

    [Column("topic_hint")]
    public string? TopicHint { get; set; }

    [Column("first_occurred_at")]
    public DateTime? FirstOccurredAt { get; set; }

    [Column("last_occurred_at")]
    public DateTime? LastOccurredAt { get; set; }

    [Column("is_resolved")]
    public bool IsResolved { get; set; } = false;

    [Column("resolved_at")]
    public DateTime? ResolvedAt { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("StudentId")]
    public virtual User? Student { get; set; }
}
