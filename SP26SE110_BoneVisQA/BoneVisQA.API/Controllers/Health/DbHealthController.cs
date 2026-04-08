using System.Threading.Tasks;
using BoneVisQA.Repositories.DBContext;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.API.Controllers.Health;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class DbHealthController : ControllerBase
{
    private readonly BoneVisQADbContext _context;

    public DbHealthController(BoneVisQADbContext context)
    {
        _context = context;
    }

    [HttpGet("check")]
    public async Task<IActionResult> Check()
    {
        try
        {
            var canConnect = await _context.Database.CanConnectAsync();

            if (canConnect)
            {
                return Ok(new { status = "ok", message = "Supabase connection successful." });
            }

            return StatusCode(500, new { status = "error", message = "Cannot connect to Supabase." });
        }
        catch (DbUpdateException)
        {
            return StatusCode(500, new { status = "error", message = "Error connecting to Supabase." });
        }
        catch (System.Exception ex)
        {
            return StatusCode(500, new { status = "error", message = ex.Message });
        }
    }
}
