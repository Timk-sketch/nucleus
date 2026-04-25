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
        string dbStatus;
        try
        {
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3));
            await db.Database.ExecuteSqlRawAsync("SELECT 1", cts.Token);
            dbStatus = "connected";
        }
        catch
        {
            dbStatus = "unreachable";
        }
        // Always return 200 — Railway healthcheck only cares that the app is alive
        return Ok(new { status = "ok", db = dbStatus, timestamp = DateTimeOffset.UtcNow });
    }
}
