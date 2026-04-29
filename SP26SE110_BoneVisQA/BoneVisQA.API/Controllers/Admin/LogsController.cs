using BoneVisQA.Repositories.DBContext;
using BoneVisQA.Repositories.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.API.Controllers.Admin;

[ApiController]
[Route("api/admin/logs")]
[Authorize(Roles = "Admin")]
public class LogsController : ControllerBase
{
    private readonly BoneVisQADbContext _context;

    public LogsController(BoneVisQADbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public ActionResult GetLogs(
        [FromQuery] string? search = null,
        [FromQuery] string? level = null,
        [FromQuery] string? category = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = _context.SystemLogs.AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            search = search.ToLower();
            query = query.Where(l => 
                l.Message.ToLower().Contains(search) || 
                (l.UserEmail != null && l.UserEmail.ToLower().Contains(search)));
        }

        if (!string.IsNullOrEmpty(level) && level != "All")
        {
            query = query.Where(l => l.Level.ToLower() == level.ToLower());
        }

        if (!string.IsNullOrEmpty(category) && category != "All")
        {
            query = query.Where(l => l.Category.ToLower() == category.ToLower());
        }

        var total = query.Count();
        var items = query
            .OrderByDescending(l => l.Timestamp)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new
            {
                id = l.Id,
                timestamp = l.Timestamp,
                level = l.Level,
                category = l.Category,
                message = l.Message,
                user = l.UserEmail,
                ip = l.IpAddress
            })
            .ToList();

        return Ok(new
        {
            success = true,
            data = new
            {
                items,
                total,
                pageIndex,
                pageSize,
                totalPages = (int)Math.Ceiling(total / (double)pageSize)
            }
        });
    }

    [HttpGet("stats")]
    public ActionResult GetStats()
    {
        var today = DateTime.UtcNow.Date;
        var todayLogs = _context.SystemLogs.Where(l => l.Timestamp.Date == today).ToList();

        return Ok(new
        {
            success = true,
            data = new
            {
                total = _context.SystemLogs.Count(),
                errors = _context.SystemLogs.Count(l => l.Level == "Error"),
                warnings = _context.SystemLogs.Count(l => l.Level == "Warning"),
                today = todayLogs.Count,
                todayErrors = todayLogs.Count(l => l.Level == "Error"),
                todayWarnings = todayLogs.Count(l => l.Level == "Warning")
            }
        });
    }

    [HttpGet("levels")]
    public ActionResult GetLevels()
    {
        return Ok(new { success = true, data = new[] { "Info", "Warning", "Error", "Success" } });
    }

    [HttpGet("categories")]
    public ActionResult GetCategories()
    {
        return Ok(new { success = true, data = new[] { "Auth", "System", "Database", "API", "Email" } });
    }

    [HttpPost]
    public ActionResult CreateLog([FromBody] CreateLogRequest request)
    {
        var log = new SystemLog
        {
            Timestamp = DateTime.UtcNow,
            Level = request.Level ?? "Info",
            Category = request.Category ?? "System",
            Message = request.Message,
            UserEmail = request.UserEmail,
            IpAddress = request.IpAddress
        };

        _context.SystemLogs.Add(log);
        _context.SaveChanges();

        return Ok(new { success = true, data = log });
    }
}

public class CreateLogRequest
{
    public string? Level { get; set; }
    public string? Category { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? UserEmail { get; set; }
    public string? IpAddress { get; set; }
}
