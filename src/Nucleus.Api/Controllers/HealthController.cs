using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;

namespace Nucleus.Api.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    private readonly INucleusDbContext _db;

    public HealthController(INucleusDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Health(CancellationToken ct)
    {
        try
        {
            // Verify DB connectivity
            await _db.Tenants.CountAsync(ct);
            return Ok(new
            {
                status = "healthy",
                service = "nucleus",
                version = "1.0.0",
                timestamp = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new
            {
                status = "unhealthy",
                error = ex.Message,
                timestamp = DateTimeOffset.UtcNow
            });
        }
    }
}
