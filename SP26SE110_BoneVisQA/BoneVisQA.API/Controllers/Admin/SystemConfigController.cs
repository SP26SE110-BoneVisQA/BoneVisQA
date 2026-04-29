using BoneVisQA.Repositories.DBContext;
using BoneVisQA.Repositories.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.API.Controllers.Admin;

[ApiController]
[Route("api/admin/system-config")]
[Authorize(Roles = "Admin")]
public class SystemConfigController : ControllerBase
{
    private readonly BoneVisQADbContext _context;

    public SystemConfigController(BoneVisQADbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public ActionResult<SystemConfigResponse> GetAll()
    {
        var configs = _context.SystemConfigs.ToList();
        var grouped = configs
            .GroupBy(c => c.Category)
            .ToDictionary(g => g.Key, g => g.Select(c => new ConfigItemResponse
            {
                Key = c.ConfigKey,
                Value = c.ConfigValue,
                Category = c.Category,
                Type = c.ValueType,
                Description = c.Description
            }).ToList());

        return Ok(new SystemConfigResponse
        {
            Success = true,
            Data = grouped
        });
    }

    [HttpGet("category/{category}")]
    public ActionResult GetByCategory(string category)
    {
        var items = _context.SystemConfigs
            .Where(c => c.Category.ToLower() == category.ToLower())
            .Select(c => new ConfigItemResponse
            {
                Key = c.ConfigKey,
                Value = c.ConfigValue,
                Category = c.Category,
                Type = c.ValueType,
                Description = c.Description
            })
            .ToList();

        if (!items.Any())
            return NotFound(new { message = $"Category '{category}' not found" });

        return Ok(new { success = true, data = items });
    }

    [HttpPut]
    public ActionResult Update([FromBody] UpdateConfigRequest request)
    {
        var config = _context.SystemConfigs.FirstOrDefault(c => c.ConfigKey == request.Key);
        
        if (config == null)
            return NotFound(new { message = $"Config key '{request.Key}' not found" });

        config.ConfigValue = request.Value;
        config.UpdatedAt = DateTime.UtcNow;
        _context.SaveChanges();

        return Ok(new { success = true, message = "Configuration updated" });
    }

    [HttpPut("batch")]
    public ActionResult UpdateBatch([FromBody] UpdateConfigBatchRequest request)
    {
        var updated = new List<string>();
        var notFound = new List<string>();

        foreach (var item in request.Configs)
        {
            var config = _context.SystemConfigs.FirstOrDefault(c => c.ConfigKey == item.Key);
            if (config != null)
            {
                config.ConfigValue = item.Value;
                config.UpdatedAt = DateTime.UtcNow;
                updated.Add(item.Key);
            }
            else
            {
                notFound.Add(item.Key);
            }
        }

        _context.SaveChanges();

        return Ok(new
        {
            success = true,
            message = $"{updated.Count} configurations updated",
            updated,
            notFound = notFound.Any() ? notFound : null
        });
    }

    [HttpPost("reset")]
    public ActionResult ResetToDefaults()
    {
        var configs = _context.SystemConfigs.ToList();
        
        var defaults = new Dictionary<string, string>
        {
            ["siteName"] = "BoneVisQA",
            ["supportEmail"] = "support@bonevisqa.com",
            ["maxUploadSize"] = "20",
            ["sessionTimeout"] = "30",
            ["smtpHost"] = "smtp.gmail.com",
            ["smtpPort"] = "587",
            ["fromEmail"] = "bonevisqasp26se110@gmail.com",
            ["emailNotifications"] = "true",
            ["passwordMinLength"] = "6",
            ["sessionDuration"] = "60",
            ["maxLoginAttempts"] = "5",
            ["timezone"] = "Asia/Ho_Chi_Minh",
            ["dateFormat"] = "DD/MM/YYYY",
            ["language"] = "vi"
        };

        foreach (var config in configs)
        {
            if (defaults.ContainsKey(config.ConfigKey))
            {
                config.ConfigValue = defaults[config.ConfigKey];
                config.UpdatedAt = DateTime.UtcNow;
            }
        }

        _context.SaveChanges();
        return Ok(new { success = true, message = "Configs reset to defaults" });
    }
}

public class ConfigItemResponse
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class SystemConfigResponse
{
    public bool Success { get; set; }
    public Dictionary<string, List<ConfigItemResponse>>? Data { get; set; }
}

public class UpdateConfigRequest
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class UpdateConfigBatchRequest
{
    public List<ConfigItemRequest> Configs { get; set; } = new();
}

public class ConfigItemRequest
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
