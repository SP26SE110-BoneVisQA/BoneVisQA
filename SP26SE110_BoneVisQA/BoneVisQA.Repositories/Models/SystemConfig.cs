using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BoneVisQA.Repositories.Models;

[Table("system_configs")]
public class SystemConfig
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("config_key")]
    public string ConfigKey { get; set; } = string.Empty;

    [Required]
    [Column("config_value")]
    public string ConfigValue { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("category")]
    public string Category { get; set; } = "General";

    [Required]
    [MaxLength(20)]
    [Column("value_type")]
    public string ValueType { get; set; } = "string";

    [MaxLength(500)]
    [Column("description")]
    public string? Description { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
