using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.JSInterop;

namespace Nucleus.Web.Services;

public class AuthService(HttpClient http, IJSRuntime js)
{
    private const string TokenKey = "nucleus_access_token";
    private const string RefreshKey = "nucleus_refresh_token";

    // ── Token storage (localStorage) ────────────────────────────────────────

    public async Task<string?> GetAccessTokenAsync()
        => await js.InvokeAsync<string?>("localStorage.getItem", TokenKey);

    public async Task<string?> GetRefreshTokenAsync()
        => await js.InvokeAsync<string?>("localStorage.getItem", RefreshKey);

    private async Task SaveTokensAsync(string access, string refresh)
    {
        await js.InvokeVoidAsync("localStorage.setItem", TokenKey, access);
        await js.InvokeVoidAsync("localStorage.setItem", RefreshKey, refresh);
    }

    public async Task ClearTokensAsync()
    {
        await js.InvokeVoidAsync("localStorage.removeItem", TokenKey);
        await js.InvokeVoidAsync("localStorage.removeItem", RefreshKey);
    }

    // ── Auth API calls ───────────────────────────────────────────────────────

    public async Task<AuthResult> RegisterAsync(
        string email, string password, string firstName, string lastName, string companyName)
    {
        var resp = await http.PostAsJsonAsync("api/v1/auth/register", new
        {
            email, password, firstName, lastName, companyName
        });

        return await ParseAuthResponse(resp);
    }

    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        var resp = await http.PostAsJsonAsync("api/v1/auth/login", new { email, password });
        return await ParseAuthResponse(resp);
    }

    public async Task<AuthResult> RefreshAsync()
    {
        var refreshToken = await GetRefreshTokenAsync();
        if (string.IsNullOrEmpty(refreshToken))
            return AuthResult.Fail("No refresh token stored.");

        var resp = await http.PostAsJsonAsync("api/v1/auth/refresh", new { refreshToken });
        return await ParseAuthResponse(resp);
    }

    public async Task LogoutAsync() => await ClearTokensAsync();

    // ── Claims parsing ───────────────────────────────────────────────────────

    public async Task<ClaimsPrincipal> GetCurrentUserAsync()
    {
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token))
            return new ClaimsPrincipal(new ClaimsIdentity());

        var claims = ParseClaimsFromJwt(token);
        var exp = claims.FirstOrDefault(c => c.Type == "exp");
        if (exp != null && long.TryParse(exp.Value, out var expSeconds))
        {
            var expiry = DateTimeOffset.FromUnixTimeSeconds(expSeconds);
            if (expiry < DateTimeOffset.UtcNow)
                return new ClaimsPrincipal(new ClaimsIdentity()); // expired
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<AuthResult> ParseAuthResponse(HttpResponseMessage resp)
    {
        var json = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            return AuthResult.Fail(ExtractError(json));

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Supports both { data: { accessToken, refreshToken } } and flat
        var data = root.TryGetProperty("data", out var d) ? d : root;

        var access = data.GetProperty("accessToken").GetString() ?? "";
        var refresh = data.GetProperty("refreshToken").GetString() ?? "";
        await SaveTokensAsync(access, refresh);
        return AuthResult.Ok();
    }

    private static string ExtractError(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out var e)) return e.GetString() ?? "Unknown error";
            if (doc.RootElement.TryGetProperty("errors", out var errs)) return errs.ToString();
        }
        catch { }
        return "Request failed";
    }

    private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length != 3) return [];

        var payload = parts[1];
        // Add padding
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        var bytes = Convert.FromBase64String(payload);

        using var doc = JsonDocument.Parse(bytes);
        return doc.RootElement.EnumerateObject()
            .Select(p => new Claim(p.Name, p.Value.ToString()))
            .ToList();
    }
}

public record AuthResult(bool Succeeded, string? Error)
{
    public static AuthResult Ok() => new(true, null);
    public static AuthResult Fail(string error) => new(false, error);
}
