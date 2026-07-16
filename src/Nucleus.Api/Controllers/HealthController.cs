using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Nucleus.Infrastructure.Data;

namespace Nucleus.Api.Controllers;

[ApiController]
[Route("health")]
public class HealthController(NucleusDbContext db, IConfiguration config) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get()
    {
        string dbStatus;
        try
        {
            // Use a fresh NpgsqlConnection that bypasses the EF Core pool entirely.
            // ExecuteSqlRawAsync(..., cancelToken) cancels command execution but NOT
            // pool slot waiting — under EMAXCONN all 10 slots are busy and the check
            // hangs until Railway times out the healthcheck. A direct connection with
            // Timeout=3 fails fast at the TCP/auth layer instead.
            var rawConn = config.GetConnectionString("DefaultConnection")
                ?? config["NUCLEUS_DB_CONNECTION"]
                ?? config["DATABASE_URL"]
                ?? "";
            var csb = new NpgsqlConnectionStringBuilder(rawConn)
            {
                Timeout = 3, // Npgsql 9: connection timeout in seconds
            };
            using var conn = new NpgsqlConnection(csb.ConnectionString);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            await conn.OpenAsync(cts.Token);
            await using var cmd = new NpgsqlCommand("SELECT 1", conn);
            await cmd.ExecuteScalarAsync(cts.Token);
            dbStatus = "connected";
        }
        catch
        {
            dbStatus = "unreachable";
        }
        // Always return 200 — Railway healthcheck only cares that the app is alive.
        return Ok(new { status = "ok", db = dbStatus, timestamp = DateTimeOffset.UtcNow });
    }
}
