using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[Table("categories")]
[Index("Name", Name = "categories_name_key", IsUnique = true)]
public partial class Category
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [InverseProperty("Category")]
    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();

    [InverseProperty("Category")]
    public virtual ICollection<MedicalCase> MedicalCases { get; set; } = new List<MedicalCase>();
}
