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

    [Column("expert_id")]
    public Guid? ExpertId { get; set; }

    [Column("class_specialty_id")]
    public Guid? ClassSpecialtyId { get; set; }

    [Column("focus_level")]
    [MaxLength(50)]
    public string? FocusLevel { get; set; } = "Basic";

    [Column("teaching_objectives", TypeName = "jsonb")]
    public string? TeachingObjectives { get; set; }

    [Column("target_pathology_categories", TypeName = "jsonb")]
    public string? TargetPathologyCategories { get; set; }

    [Column("target_student_level")]
    [MaxLength(50)]
    public string? TargetStudentLevel { get; set; } = "Beginner";

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [InverseProperty("Class")]
    public virtual ICollection<Announcement> Announcements { get; set; } = new List<Announcement>();

    [InverseProperty("Class")]
    public virtual ICollection<ClassCase> ClassCases { get; set; } = new List<ClassCase>();

    [InverseProperty("Class")]
    public virtual ICollection<ClassEnrollment> ClassEnrollments { get; set; } = new List<ClassEnrollment>();

    [InverseProperty("Class")]
    public virtual ICollection<ClassTag> ClassTags { get; set; } = new List<ClassTag>();

    [InverseProperty("Class")]
    public virtual ICollection<LearningStatistic> LearningStatistics { get; set; } = new List<LearningStatistic>();

    [ForeignKey("LecturerId")]
    [InverseProperty("AcademicClasses")]
    public virtual User? Lecturer { get; set; }

    [ForeignKey("ExpertId")]
    [InverseProperty("ExpertAcademicClasses")]
    public virtual User? Expert { get; set; }

    [ForeignKey("ClassSpecialtyId")]
    [InverseProperty("AcademicClasses")]
    public virtual BoneSpecialty? ClassSpecialty { get; set; }

    [InverseProperty("Class")]
    public virtual ICollection<ClassQuizSession> ClassQuizSessions { get; set; } = new List<ClassQuizSession>();

    [InverseProperty("Class")]
    public virtual ICollection<ClassExpertAssignment> ClassExpertAssignments { get; set; } = new List<ClassExpertAssignment>();

}
