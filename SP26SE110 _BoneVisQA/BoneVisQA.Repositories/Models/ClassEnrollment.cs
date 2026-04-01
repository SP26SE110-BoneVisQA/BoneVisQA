using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[Table("class_enrollments")]
[Index("ClassId", "StudentId", Name = "class_enrollments_class_id_student_id_key", IsUnique = true)]
[Index("StudentId", Name = "idx_class_enrollments_student")]
public partial class ClassEnrollment
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("class_id")]
    public Guid ClassId { get; set; }

    [Column("student_id")]
    public Guid StudentId { get; set; }

    [Column("enrolled_at")]
    public DateTime? EnrolledAt { get; set; }

    [ForeignKey("ClassId")]
    [InverseProperty("ClassEnrollments")]
    public virtual AcademicClass Class { get; set; } = null!;

    [ForeignKey("StudentId")]
    [InverseProperty("ClassEnrollments")]
    public virtual UserProfile Student { get; set; } = null!;
}
