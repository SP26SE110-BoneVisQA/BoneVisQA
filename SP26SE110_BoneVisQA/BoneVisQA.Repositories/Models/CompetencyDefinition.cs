using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace BoneVisQA.Repositories.Models;

[Table("competency_definitions")]
public class CompetencyDefinition
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("bone_specialty_id")]
    public Guid? BoneSpecialtyId { get; set; }

    [Column("pathology_category_id")]
    public Guid? PathologyCategoryId { get; set; }

    [Column("mastery_thresholds", TypeName = "jsonb")]
    public string MasteryThresholds { get; set; } = "{\"Beginner\": 0, \"Intermediate\": 40, \"Proficient\": 60, \"Expert\": 80}";

    [Column("description")]
    public string? Description { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey("BoneSpecialtyId")]
    public virtual BoneSpecialty? BoneSpecialty { get; set; }

    [ForeignKey("PathologyCategoryId")]
    public virtual PathologyCategory? PathologyCategory { get; set; }
}
