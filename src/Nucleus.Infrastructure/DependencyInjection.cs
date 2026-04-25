using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Domain.Entities;
using Nucleus.Infrastructure.Auth;
using Nucleus.Infrastructure.Persistence;

namespace Nucleus.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        // EF Core + Supabase (PostgreSQL)
        services.AddDbContext<NucleusDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(NucleusDbContext).Assembly.FullName);
                npgsql.EnableRetryOnFailure(3);
            });
        });
        services.AddScoped<INucleusDbContext>(sp => sp.GetRequiredService<NucleusDbContext>());

        // ASP.NET Core Identity
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = false;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<NucleusDbContext>()
        .AddDefaultTokenProviders();

        // JWT token service
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        // Hangfire
        services.AddHangfire(hf => hf
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(connectionString)));

        services.AddHangfireServer(options =>
        {
            options.WorkerCount = Environment.ProcessorCount * 2;
        });

        // HTTP clients for external APIs (configured via named clients)
        services.AddHttpClient();

        return services;
    }
}
