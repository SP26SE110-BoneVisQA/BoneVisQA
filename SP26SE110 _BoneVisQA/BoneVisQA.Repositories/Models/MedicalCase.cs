using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

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

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [InverseProperty("Case")]
    public virtual ICollection<CaseTag> CaseTags { get; set; } = new List<CaseTag>();

    [InverseProperty("Case")]
    public virtual ICollection<CaseViewLog> CaseViewLogs { get; set; } = new List<CaseViewLog>();

    [ForeignKey("CategoryId")]
    [InverseProperty("MedicalCases")]
    public virtual Category? Category { get; set; }

    [InverseProperty("Case")]
    public virtual ICollection<MedicalImage> MedicalImages { get; set; } = new List<MedicalImage>();

    [InverseProperty("Case")]
    public virtual ICollection<QuizQuestion> QuizQuestions { get; set; } = new List<QuizQuestion>();

    [InverseProperty("Case")]
    public virtual ICollection<StudentQuestion> StudentQuestions { get; set; } = new List<StudentQuestion>();
}
