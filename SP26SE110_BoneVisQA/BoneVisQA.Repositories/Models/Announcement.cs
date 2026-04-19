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
    public string Title { get; set; } = null!;

    [Column("content")]
    public string Content { get; set; } = null!;

    [Column("send_email")]
    public bool? SendEmail { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("ClassId")]
    [InverseProperty("Announcements")]
    public virtual AcademicClass Class { get; set; } = null!;
}
