using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BoneVisQA.Repositories.Models;

[Table("backups")]
public class Backup
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("type")]
    public string Type { get; set; } = "all";

    [MaxLength(50)]
    [Column("size")]
    public string? Size { get; set; }

    [MaxLength(50)]
    [Column("status")]
    public string Status { get; set; } = "pending";

    [MaxLength(255)]
    [Column("file_path")]
    public string? FilePath { get; set; }

    [MaxLength(500)]
    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_by")]
    public Guid? CreatedBy { get; set; }

    [ForeignKey("CreatedBy")]
    public User? Creator { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }
}

[Table("data_exports")]
public class DataExport
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("export_type")]
    public string ExportType { get; set; } = "all";

    [Required]
    [MaxLength(10)]
    [Column("format")]
    public string Format { get; set; } = "csv";

    [Column("record_count")]
    public int RecordCount { get; set; }

    [MaxLength(50)]
    [Column("status")]
    public string Status { get; set; } = "pending";

    [MaxLength(255)]
    [Column("file_path")]
    public string? FilePath { get; set; }

    [Column("created_by")]
    public Guid? CreatedBy { get; set; }

    [ForeignKey("CreatedBy")]
    public User? Creator { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }
}
