using System.Text;
using AspNetCoreRateLimit;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Nucleus.Api.Middleware;
using Nucleus.Application;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog ────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

// ── Application + Infrastructure layers ───────────────────────────────────
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ── Tenant service (scoped, written to by TenantMiddleware) ────────────────
builder.Services.AddScoped<CurrentTenantServiceHolder>();
builder.Services.AddScoped<ICurrentTenantService>(sp => sp.GetRequiredService<CurrentTenantServiceHolder>());
builder.Services.AddScoped<ICurrentTenantServiceSetter>(sp => sp.GetRequiredService<CurrentTenantServiceHolder>());

// ── JWT Authentication ─────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "nucleus",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "nucleus",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };
    });

builder.Services.AddAuthorization();

// ── Rate Limiting ──────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.EnableEndpointRateLimiting = true;
    options.GeneralRules = new List<RateLimitRule>
    {
        new() { Endpoint = "*", Period = "1m", Limit = 600 },       // authenticated
        new() { Endpoint = "*/auth/*", Period = "1m", Limit = 20 }, // auth endpoints stricter
    };
});
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

// ── Controllers + Swagger ──────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Nucleus API",
        Version = "v1",
        Description = "Multi-brand Marketing OS — powered by Nucleus"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ── CORS ───────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ── SignalR ────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

var app = builder.Build();

// ── Middleware pipeline ────────────────────────────────────────────────────
app.UseSerilogRequestLogging();
app.UseIpRateLimiting();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantMiddleware>();

app.MapControllers();
app.MapHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthFilter() }
});

// ── Create DB schema on startup (Sprint 1 — no migration files yet) ────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<Nucleus.Infrastructure.Persistence.NucleusDbContext>();
    db.Database.EnsureCreated();
}

app.Run();
