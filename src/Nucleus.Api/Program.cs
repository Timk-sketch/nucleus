using System.Text;
using FluentValidation;
using Hangfire;
using Hangfire.PostgreSql;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
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
})
.AddEntityFrameworkStores<NucleusDbContext>()
.AddDefaultTokenProviders();

// ── JWT Auth ──────────────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["Jwt:Key"]
    ?? builder.Configuration["JWT_SECRET"]
    ?? throw new InvalidOperationException("JWT secret not set (Jwt__Key or JWT_SECRET)");

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

// ── CORS (dev-friendly) ───────────────────────────────────────────────────
builder.Services.AddCors(opts =>
    opts.AddPolicy("NucleusPolicy", p => p
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .SetIsOriginAllowed(_ => true)));

var app = builder.Build();

app.UseBlazorFrameworkFiles();    // serve Blazor WASM _framework/ files
app.UseStaticFiles();              // serve wwwroot static assets

app.UseMiddleware<TenantMiddleware>();
app.UseSerilogRequestLogging();
app.UseCors("NucleusPolicy");
app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Nucleus v1"));

app.MapControllers();
app.MapHub<ProvisioningHub>("/hubs/provisioning");
app.MapHangfireDashboard("/hangfire");
app.MapFallbackToFile("index.html"); // SPA client-side routing fallback

// EnsureCreated runs in background so the app starts listening immediately
// (Railway healthcheck needs a fast response — don't block on DB init)
_ = Task.Run(async () =>
{
    await Task.Delay(1000); // brief pause so DI is fully warm
    using var scope = app.Services.CreateScope();
    var dbCtx = scope.ServiceProvider.GetRequiredService<NucleusDbContext>();
    await dbCtx.Database.EnsureCreatedAsync();
});

app.Run();
