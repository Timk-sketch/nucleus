using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nucleus.Infrastructure.Data;

namespace Nucleus.Api.Controllers;

[ApiController]
[Route("health")]
public class HealthController(NucleusDbContext db) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get()
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync("SELECT 1");
            return Ok(new { status = "healthy", db = "connected", timestamp = DateTimeOffset.UtcNow });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { status = "unhealthy", db = "disconnected", error = ex.Message });
        }
    }
}
