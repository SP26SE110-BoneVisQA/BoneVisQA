using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[Table("tags")]
[Index("Type", Name = "idx_tags_type")]
[Index("Name", "Type", Name = "tags_name_type_key", IsUnique = true)]
public partial class Tag
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("type")]
    public string Type { get; set; } = null!;

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [InverseProperty("Tag")]
    public virtual ICollection<CaseTag> CaseTags { get; set; } = new List<CaseTag>();
}
