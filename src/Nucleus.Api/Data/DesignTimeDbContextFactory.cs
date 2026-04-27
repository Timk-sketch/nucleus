using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Infrastructure.Data;

namespace Nucleus.Api.Data;

/// <summary>
/// Allows `dotnet ef migrations add` to instantiate NucleusDbContext at design time
/// without a running web host.
///
/// Usage:
///   cd src/Nucleus.Api
///   dotnet ef migrations add Initial --project ../Nucleus.Infrastructure
///   dotnet ef database update
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<NucleusDbContext>
{
    public NucleusDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connStr = config.GetConnectionString("DefaultConnection")
            ?? config["NUCLEUS_DB_CONNECTION"]
            ?? config["DATABASE_URL"]
            ?? throw new InvalidOperationException(
                "Set NUCLEUS_DB_CONNECTION or DefaultConnection to run EF tooling.");

        if (connStr.StartsWith("postgres", StringComparison.OrdinalIgnoreCase))
        {
            var u = new Uri(connStr);
            var colonIdx = u.UserInfo.IndexOf(':');
            var username = colonIdx >= 0 ? u.UserInfo[..colonIdx] : u.UserInfo;
            var password = colonIdx >= 0 ? Uri.UnescapeDataString(u.UserInfo[(colonIdx + 1)..]) : "";
            connStr = $"Host={u.Host};Port={u.Port};Database={u.AbsolutePath.TrimStart('/')};Username={username};Password={password};SSL Mode=Prefer;Trust Server Certificate=true";
        }

        var opts = new DbContextOptionsBuilder<NucleusDbContext>()
            .UseNpgsql(connStr)
            .Options;

        // Design-time: use Guid.Empty tenant (filters not applied during migration)
        return new NucleusDbContext(opts, new NullTenantService());
    }

    private class NullTenantService : ICurrentTenantService
    {
        public Guid TenantId => Guid.Empty;
        public Guid UserId => Guid.Empty;
        public string[] Roles => [];
        public bool IsAuthenticated => false;
    }
}
