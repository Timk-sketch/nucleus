using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Nucleus.Application.Behaviors;
using Nucleus.Api.Hubs;
using Nucleus.Api.Jobs;
using Nucleus.Api.Middleware;
using Nucleus.Domain.Entities;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Infrastructure.Auth;
using Nucleus.Infrastructure.Data;
using Nucleus.Infrastructure.Email;
using Nucleus.Infrastructure.Jobs;
using Nucleus.Infrastructure.Multitenancy;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── Port (Railway injects PORT env var) ───────────────────────────────────
var port = builder.Configuration["PORT"] ?? "8080";
builder.WebHost.UseUrls($"http://+:{port}");

// ── Serilog ───────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

// ── Sentry ────────────────────────────────────────────────────────────────
var sentryDsn = builder.Configuration["SENTRY_DSN"];
if (!string.IsNullOrEmpty(sentryDsn))
{
    builder.WebHost.UseSentry(o =>
    {
        o.Dsn = sentryDsn;
        o.TracesSampleRate = 0.1;
        o.SendDefaultPii = false;
    });
}

// ── Database ──────────────────────────────────────────────────────────────
// Priority: appsettings → NUCLEUS_DB_CONNECTION (Supabase) → DATABASE_URL (Railway Postgres plugin)
var rawConnStr = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration["NUCLEUS_DB_CONNECTION"]
    ?? builder.Configuration["DATABASE_URL"];

if (rawConnStr is null)
    throw new InvalidOperationException("Connection string not set. Set NUCLEUS_DB_CONNECTION or add a Railway PostgreSQL plugin.");

// Railway DATABASE_URL is a postgres:// URI — convert to Npgsql key=value format
var connectionString = rawConnStr.StartsWith("postgres", StringComparison.OrdinalIgnoreCase)
    ? ConvertPostgresUri(rawConnStr)
    : rawConnStr;

static string ConvertPostgresUri(string uri)
{
    var u = new Uri(uri);
    var colonIdx = u.UserInfo.IndexOf(':');
    var username = colonIdx >= 0 ? u.UserInfo[..colonIdx] : u.UserInfo;
    var password = colonIdx >= 0 ? Uri.UnescapeDataString(u.UserInfo[(colonIdx + 1)..]) : "";
    // SSL Mode=Prefer: uses SSL when the server supports it (Railway internal does not require it)
    return $"Host={u.Host};Port={u.Port};Database={u.AbsolutePath.TrimStart('/')};Username={username};Password={password};SSL Mode=Prefer;Trust Server Certificate=true";
}

builder.Services.AddDbContext<NucleusDbContext>(opts =>
    opts.UseNpgsql(connectionString));
builder.Services.AddScoped<INucleusDbContext>(sp => sp.GetRequiredService<NucleusDbContext>());

// ── ASP.NET Identity ──────────────────────────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(opts =>
{
    opts.Password.RequireDigit = true;
    opts.Password.RequiredLength = 8;
    opts.Password.RequireNonAlphanumeric = false;
    opts.User.RequireUniqueEmail = true;
    // Account lockout: 5 failed attempts → 15 min lockout
    opts.Lockout.MaxFailedAccessAttempts = 5;
    opts.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    opts.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<NucleusDbContext>()
.AddDefaultTokenProviders();

// ── Credential encryption (AES-256) ───────────────────────────────────────
var encKeyStr = builder.Configuration["CREDENTIAL_ENCRYPTION_KEY"];
if (!string.IsNullOrEmpty(encKeyStr))
{
    EncryptedStringConverter.SetEncryptionKey(Convert.FromBase64String(encKeyStr));
}
else if (!builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "CREDENTIAL_ENCRYPTION_KEY is required in production. " +
        "Generate one with: openssl rand -base64 32");
}

// ── JWT Auth ──────────────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["Jwt:Key"]
    ?? builder.Configuration["JWT_SECRET"]
    ?? throw new InvalidOperationException("JWT secret not set (Jwt__Key or JWT_SECRET)");

if (jwtSecret.Length < 32)
    throw new InvalidOperationException(
        "JWT_SECRET must be at least 32 characters for HS256 security.");

builder.Services.AddAuthentication(opts =>
{
    opts.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    opts.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(opts =>
{
    opts.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = false,
        ValidateAudience = false,
        ClockSkew = TimeSpan.FromSeconds(30),
    };
    // Allow JWT via SignalR query string
    opts.Events = new JwtBearerEvents
    {
        OnMessageReceived = ctx =>
        {
            var token = ctx.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                ctx.Token = token;
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddAuthorization();

// ── Multi-tenancy ─────────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentTenantService, CurrentTenantService>();

// ── MediatR + FluentValidation ────────────────────────────────────────────
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Nucleus.Application.Behaviors.ValidationBehavior<,>).Assembly));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddValidatorsFromAssembly(typeof(Nucleus.Application.Behaviors.ValidationBehavior<,>).Assembly);

// ── Hangfire ──────────────────────────────────────────────────────────────
builder.Services.AddHangfire(cfg => cfg
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(opts => opts.UseNpgsqlConnection(connectionString)));
builder.Services.AddHangfireServer();
builder.Services.AddScoped<IBackgroundJobService, HangfireBackgroundJobService>();
builder.Services.AddScoped<BrandProvisioningJob>();
builder.Services.AddScoped<GhlContactSyncJob>();
builder.Services.AddScoped<KeywordRankJob>();

// ── HTTP clients ──────────────────────────────────────────────────────────
builder.Services.AddHttpClient("provisioning", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddHttpClient("wordpress", client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.Add("User-Agent", "Nucleus/1.0");
});
builder.Services.AddHttpClient("ghl", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "Nucleus/1.0");
});
builder.Services.AddHttpClient("dataforseo", client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
    client.BaseAddress = new Uri("https://api.dataforseo.com");
});

// ── Email service (MailKit) ────────────────────────────────────────────────
builder.Services.AddSingleton<IEmailService, MailKitEmailService>();

// ── SignalR ───────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ── JWT token service ─────────────────────────────────────────────────────
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<JwtTokenService>();

// ── Swagger ───────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Nucleus API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "JWT Bearer token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }] = []
    });
});

builder.Services.AddControllers();

// ── Health checks ─────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddNpgsql(connectionString);

// ── Rate limiting ─────────────────────────────────────────────────────────
// "auth" policy: 10 requests/minute per IP on login/register/refresh endpoints
builder.Services.AddRateLimiter(opts =>
{
    opts.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
        }));
    opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opts.OnRejected = async (ctx, _) =>
    {
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsync(
            "{\"success\":false,\"error\":\"Too many requests. Please wait and try again.\"}");
    };
});

// ── CORS ──────────────────────────────────────────────────────────────────
// In dev: allow any origin. In production: restrict to ALLOWED_ORIGINS env var.
var allowedOrigins = builder.Configuration["ALLOWED_ORIGINS"]
    ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? [];

builder.Services.AddCors(opts =>
    opts.AddPolicy("NucleusPolicy", p =>
    {
        if (builder.Environment.IsDevelopment() || allowedOrigins.Length == 0)
            p.SetIsOriginAllowed(_ => true);
        else
            p.WithOrigins(allowedOrigins);

        p.AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    }));

var app = builder.Build();

app.UseBlazorFrameworkFiles();    // serve Blazor WASM _framework/ files
app.UseStaticFiles();              // serve wwwroot static assets

app.UseRateLimiter();
app.UseMiddleware<TenantMiddleware>();
app.UseSerilogRequestLogging();
app.UseCors("NucleusPolicy");
app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Nucleus v1"));

app.MapControllers();
app.MapHub<ProvisioningHub>("/hubs/provisioning");
app.MapHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireAuthFilter(app.Configuration)],
});
app.MapHealthChecks("/health");
app.MapFallbackToFile("index.html"); // SPA client-side routing fallback

// DB init runs in background so Railway healthcheck gets a fast first response.
// MigrateAsync applies pending EF migrations when they exist.
// Falls back to EnsureCreated when no migrations have been added yet (dev / first deploy).
_ = Task.Run(async () =>
{
    await Task.Delay(1000); // brief pause so DI is fully warm
    using var scope = app.Services.CreateScope();
    var dbCtx = scope.ServiceProvider.GetRequiredService<NucleusDbContext>();
    try
    {
        var pending = await dbCtx.Database.GetPendingMigrationsAsync();
        if (pending.Any())
            await dbCtx.Database.MigrateAsync();
        else
            await dbCtx.Database.EnsureCreatedAsync();
    }
    catch
    {
        // No migrations registered — fall back to EnsureCreated
        await dbCtx.Database.EnsureCreatedAsync();
    }
});

app.Run();

// ── Hangfire dashboard auth (HTTP Basic in production, open on localhost) ──
public class HangfireAuthFilter(IConfiguration config) : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();

        var remote = http.Connection.RemoteIpAddress;
        if (remote != null && System.Net.IPAddress.IsLoopback(remote))
            return true;

        var authHeader = http.Request.Headers["Authorization"].FirstOrDefault();
        if (authHeader?.StartsWith("Basic ") != true)
        {
            http.Response.StatusCode = 401;
            http.Response.Headers["WWW-Authenticate"] = "Basic realm=\"Nucleus\"";
            return false;
        }

        try
        {
            var decoded = System.Text.Encoding.UTF8.GetString(
                Convert.FromBase64String(authHeader["Basic ".Length..].Trim()));
            var sep = decoded.IndexOf(':');
            if (sep < 0) return false;
            var user = decoded[..sep];
            var pass = decoded[(sep + 1)..];
            var expected = config["HANGFIRE_ADMIN_PASSWORD"];
            return !string.IsNullOrEmpty(expected) && user == "admin" && pass == expected;
        }
        catch { return false; }
    }
}
