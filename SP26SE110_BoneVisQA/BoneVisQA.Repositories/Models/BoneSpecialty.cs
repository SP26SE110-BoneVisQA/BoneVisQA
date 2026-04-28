using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BoneVisQA.Repositories.Models;

[Table("bone_specialties")]
public partial class BoneSpecialty
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("name")]
    [MaxLength(256)]
    public string Name { get; set; } = null!;

    [InverseProperty("BoneSpecialty")]
    public virtual ICollection<ExpertSpecialty> ExpertSpecialties { get; set; } = new List<ExpertSpecialty>();

    [InverseProperty("ClassSpecialty")]
    public virtual ICollection<AcademicClass> AcademicClasses { get; set; } = new List<AcademicClass>();
}
