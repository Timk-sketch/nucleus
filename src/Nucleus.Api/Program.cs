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

// ── Database (Supabase / PostgreSQL) ─────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration["NUCLEUS_DB_CONNECTION"]
    ?? throw new InvalidOperationException("Connection string not set (ConnectionStrings__DefaultConnection or NUCLEUS_DB_CONNECTION)");

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
    Authorization = [new Hangfire.Dashboard.LocalRequestsOnlyAuthorizationFilter()]
});

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
