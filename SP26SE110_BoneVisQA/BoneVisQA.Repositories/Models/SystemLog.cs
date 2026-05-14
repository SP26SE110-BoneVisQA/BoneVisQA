using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BoneVisQA.Repositories.Models;

[Table("system_logs")]
public class SystemLog
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("timestamp")]
    public DateTime Timestamp { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("level")]
    public string Level { get; set; } = "Info";

    [Required]
    [MaxLength(50)]
    [Column("category")]
    public string Category { get; set; } = "System";

    [Required]
    [MaxLength(1000)]
    [Column("message")]
    public string Message { get; set; } = string.Empty;

    [MaxLength(500)]
    [Column("user_email")]
    public string? UserEmail { get; set; }

    [MaxLength(50)]
    [Column("ip_address")]
    public string? IpAddress { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
