using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[Table("announcements")]
[Index("ClassId", Name = "idx_announcements_class")]
public partial class Announcement
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("class_id")]
    public Guid ClassId { get; set; }

    [Column("title")]
    [MaxLength(500)]
    public string Title { get; set; } = null!;

    [Column("content")]
    public string Content { get; set; } = null!;

    [Column("send_email")]
    public bool? SendEmail { get; set; }

    /// <summary>
    /// Optional: ID of the assignment (case or quiz) associated with this announcement.
    /// When set, students can quickly identify related work to complete.
    /// Note: No FK constraint because class_cases uses composite key (class_id, case_id).
    /// Application validates assignmentId in code.
    /// </summary>
    [Column("assignment_id")]
    public Guid? AssignmentId { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("ClassId")]
    [InverseProperty("Announcements")]
    public virtual AcademicClass Class { get; set; } = null!;

    /// <summary>Inverse nav: các ClassCase được tạo từ announcement này.</summary>
    [InverseProperty("Announcement")]
    public virtual ICollection<ClassCase> ClassCases { get; set; } = new List<ClassCase>();

    /// <summary>Inverse nav: các ClassQuizSession được tạo từ announcement này.</summary>
    [InverseProperty("Announcement")]
    public virtual ICollection<ClassQuizSession> ClassQuizSessions { get; set; } = new List<ClassQuizSession>();
}
