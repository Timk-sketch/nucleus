using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Nucleus.Web;
using Nucleus.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// ── API base URL ──────────────────────────────────────────────────────────
var apiBase = builder.Configuration["ApiBaseUrl"]
    ?? builder.HostEnvironment.BaseAddress;

// ── Auth services ─────────────────────────────────────────────────────────
builder.Services.AddScoped<AuthHeaderHandler>();

// Unauthenticated client — only used inside AuthService for login/register
builder.Services.AddHttpClient<AuthService>(c => c.BaseAddress = new Uri(apiBase));

// Authenticated client — used by all protected pages via @inject HttpClient
builder.Services.AddHttpClient("NucleusApi", c => c.BaseAddress = new Uri(apiBase))
    .AddHttpMessageHandler<AuthHeaderHandler>();

builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("NucleusApi"));

// ── Auth state ────────────────────────────────────────────────────────────
builder.Services.AddScoped<JwtAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(
    sp => sp.GetRequiredService<JwtAuthStateProvider>());
builder.Services.AddAuthorizationCore();

await builder.Build().RunAsync();
