using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[Table("qa_messages")]
[Index("SessionId", "CreatedAt", Name = "idx_qa_messages_session_created_at")]
[Index("Role", Name = "idx_qa_messages_role")]
public partial class QAMessage
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("session_id")]
    public Guid SessionId { get; set; }

    [Column("role")]
    [MaxLength(20)]
    public string Role { get; set; } = "User";

    [Column("content")]
    public string Content { get; set; } = string.Empty;

    [Column("coordinates", TypeName = "jsonb")]
    public string? Coordinates { get; set; }

    [Column("suggested_diagnosis")]
    public string? SuggestedDiagnosis { get; set; }

    [Column("differential_diagnoses", TypeName = "jsonb")]
    public string? DifferentialDiagnoses { get; set; }

    [Column("key_imaging_findings")]
    public string? KeyImagingFindings { get; set; }

    [Column("reflective_questions")]
    public string? ReflectiveQuestions { get; set; }

    [Column("ai_confidence_score")]
    public double? AiConfidenceScore { get; set; }

    [Column("client_request_id")]
    public string? ClientRequestId { get; set; }

    [Column("citations_json", TypeName = "jsonb")]
    public string? CitationsJson { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("SessionId")]
    [InverseProperty("Messages")]
    public virtual VisualQASession Session { get; set; } = null!;

    [InverseProperty("Message")]
    public virtual ICollection<Citation> Citations { get; set; } = new List<Citation>();
}
