using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace BoneVisQA.Repositories.Models;

[Table("medical_cases")]
public partial class MedicalCase
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("title")]
    public string Title { get; set; } = null!;

    [Column("description")]
    public string Description { get; set; } = null!;

    [Column("difficulty")]
    public string? Difficulty { get; set; }

    [Column("category_id")]
    public Guid? CategoryId { get; set; }

    [Column("created_by_expert_id")]
    public Guid? CreatedByExpertId { get; set; }

    [Column("assigned_expert_id")]
    public Guid? AssignedExpertId { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("is_approved")]

    public bool? IsApproved { get; set; } = false;

    [Column("is_active")]
    public bool? IsActive { get; set; } = true;

    [Column("suggested_diagnosis")]
    public string? SuggestedDiagnosis { get; set; }

    [Column("key_findings")]
    public string? KeyFindings { get; set; }

    [Column("reflective_questions")]
    public string? ReflectiveQuestions { get; set; }

    /// <summary>Semantic embedding for RAG (768-dim Gemini); populated by background indexer.</summary>
    [Column("embedding", TypeName = "vector(768)")]
    public Vector? Embedding { get; set; }

    /// <summary>Pipeline state: Pending, Processing, Completed, Failed.</summary>
    [Column("indexing_status")]
    public string IndexingStatus { get; set; } = "Pending";

    /// <summary>Semantic version (Major.Minor.Patch) for case content/index refreshes.</summary>
    [Column("version")]
    public string? Version { get; set; } = "1.0.0";

    [InverseProperty("Case")]
    public virtual ICollection<ClassCase> ClassCases { get; set; } = new List<ClassCase>();

    [InverseProperty("Case")]
    public virtual ICollection<CaseTag> CaseTags { get; set; } = new List<CaseTag>();

    [InverseProperty("Case")]
    public virtual ICollection<CaseViewLog> CaseViewLogs { get; set; } = new List<CaseViewLog>();

    [ForeignKey("CategoryId")]
    [InverseProperty("MedicalCases")]
    public virtual Category? Category { get; set; }

    [ForeignKey("CreatedByExpertId")]
    [InverseProperty("CreatedMedicalCases")]
    public virtual User? CreatedByExpert { get; set; }

    [ForeignKey("AssignedExpertId")]
    [InverseProperty("AssignedMedicalCases")]
    public virtual User? AssignedExpert { get; set; }

    [InverseProperty("Case")]
    public virtual ICollection<MedicalImage> MedicalImages { get; set; } = new List<MedicalImage>();

    [InverseProperty("Case")]
    public virtual ICollection<QuizQuestion> QuizQuestions { get; set; } = new List<QuizQuestion>();

    [InverseProperty("Case")]
    public virtual ICollection<StudentQuestion> StudentQuestions { get; set; } = new List<StudentQuestion>();

    [InverseProperty("Case")]
    public virtual ICollection<VisualQASession> VisualQASessions { get; set; } = new List<VisualQASession>();
}
