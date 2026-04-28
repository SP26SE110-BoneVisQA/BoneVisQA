using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BoneVisQA.Repositories.Models;

/// <summary>Junction: expert user ↔ medical specialty focus (e.g. Spine, Trauma).</summary>
[Table("expert_specialties")]
public partial class ExpertSpecialty
{
    [Column("expert_id")]
    public Guid ExpertId { get; set; }

    [Column("bone_specialty_id")]
    public Guid BoneSpecialtyId { get; set; }

    [ForeignKey("ExpertId")]
    [InverseProperty("ExpertSpecialties")]
    public virtual User Expert { get; set; } = null!;

    [ForeignKey("BoneSpecialtyId")]
    [InverseProperty("ExpertSpecialties")]
    public virtual BoneSpecialty BoneSpecialty { get; set; } = null!;
}
