using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[Table("academic_classes")]
public partial class AcademicClass
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("class_name")]
    public string ClassName { get; set; } = null!;

    [Column("semester")]
    public string Semester { get; set; } = null!;

    [Column("lecturer_id")]
    public Guid? LecturerId { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [InverseProperty("Class")]
    public virtual ICollection<Announcement> Announcements { get; set; } = new List<Announcement>();

    [InverseProperty("Class")]
    public virtual ICollection<ClassEnrollment> ClassEnrollments { get; set; } = new List<ClassEnrollment>();

    [InverseProperty("Class")]
    public virtual ICollection<LearningStatistic> LearningStatistics { get; set; } = new List<LearningStatistic>();

    [ForeignKey("LecturerId")]
    [InverseProperty("AcademicClasses")]
    public virtual UserProfile? Lecturer { get; set; }

    [InverseProperty("Class")]
    public virtual ICollection<Quiz> Quizzes { get; set; } = new List<Quiz>();
}
