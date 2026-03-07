using System.Threading.Tasks;
using BoneVisQA.Repositories.DBContext;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.API.Controllers;

[ApiController]
[Route("api/[controller]")]
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
                return Ok(new { status = "ok", message = "Kết nối Supabase thành công." });
            }

            return StatusCode(500, new { status = "error", message = "Không kết nối được đến Supabase." });
        }
        catch (DbUpdateException)
        {
            return StatusCode(500, new { status = "error", message = "Lỗi khi kết nối đến Supabase." });
        }
        catch (System.Exception ex)
        {
            return StatusCode(500, new { status = "error", message = ex.Message });
        }
    }
}

