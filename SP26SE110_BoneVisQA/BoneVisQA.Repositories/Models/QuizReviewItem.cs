using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace BoneVisQA.Repositories.Models;

[Table("quiz_review_items")]
public class QuizReviewItem
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("attempt_id")]
    public Guid AttemptId { get; set; }

    [Column("question_id")]
    public Guid QuestionId { get; set; }

    [Column("question_text")]
    public string QuestionText { get; set; } = string.Empty;

    [Column("student_answer")]
    public string? StudentAnswer { get; set; }

    [Column("correct_answer")]
    public string? CorrectAnswer { get; set; }

    [Column("is_correct")]
    public bool? IsCorrect { get; set; }

    [Column("ai_explanation")]
    public string? AiExplanation { get; set; }

    [Column("related_cases", TypeName = "jsonb")]
    public string RelatedCases { get; set; } = "[]";

    [Column("topic_tags", TypeName = "jsonb")]
    public string TopicTags { get; set; } = "[]";

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("AttemptId")]
    public virtual QuizAttempt? Attempt { get; set; }

    [ForeignKey("QuestionId")]
    public virtual QuizQuestion? Question { get; set; }

    [NotMapped]
    public List<Guid> RelatedCaseIds
    {
        get => string.IsNullOrEmpty(RelatedCases) 
            ? new List<Guid>() 
            : JsonConvert.DeserializeObject<List<Guid>>(RelatedCases) ?? new List<Guid>();
        set => RelatedCases = JsonConvert.SerializeObject(value);
    }

    [NotMapped]
    public List<string> TopicTagList
    {
        get => string.IsNullOrEmpty(TopicTags) 
            ? new List<string>() 
            : JsonConvert.DeserializeObject<List<string>>(TopicTags) ?? new List<string>();
        set => TopicTags = JsonConvert.SerializeObject(value);
    }
}
