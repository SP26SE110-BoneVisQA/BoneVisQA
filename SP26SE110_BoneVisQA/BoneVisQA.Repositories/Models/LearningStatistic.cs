using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[Table("learning_statistics")]
[Index("StudentId", "ClassId", Name = "learning_statistics_student_id_class_id_key", IsUnique = true)]
public partial class LearningStatistic
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("student_id")]
    public Guid StudentId { get; set; }

    [Column("class_id")]
    public Guid? ClassId { get; set; }

    [Column("total_cases_viewed")]
    public int? TotalCasesViewed { get; set; }

    [Column("total_questions_asked")]
    public int? TotalQuestionsAsked { get; set; }

    [Column("avg_quiz_score")]
    public double? AvgQuizScore { get; set; }

    [Column("error_distribution", TypeName = "jsonb")]
    public string? ErrorDistribution { get; set; }

    [Column("last_updated")]
    public DateTime? LastUpdated { get; set; }

    [ForeignKey("ClassId")]
    [InverseProperty("LearningStatistics")]
    public virtual AcademicClass? Class { get; set; }

    [ForeignKey("StudentId")]
    [InverseProperty("LearningStatistics")]
    public virtual User Student { get; set; } = null!;
}
