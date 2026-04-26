namespace Nucleus.Web.Services;

/// <summary>
/// Delegating handler that:
/// 1. Proactively refreshes the token if within 60s of expiry
/// 2. Attaches Bearer token to every outgoing request
/// 3. On 401, attempts one silent token refresh then retries
/// SemaphoreSlim prevents concurrent refresh storms.
/// </summary>
public class AuthHeaderHandler(AuthService authService, JwtAuthStateProvider authState)
    : DelegatingHandler
{
    private static readonly SemaphoreSlim _refreshLock = new(1, 1);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await GetValidTokenAsync();
        SetBearer(request, token);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            var newToken = await RefreshAndGetTokenAsync();
            if (newToken != null)
            {
                var retry = await CloneRequestAsync(request);
                SetBearer(retry, newToken);
                response.Dispose();
                response = await base.SendAsync(retry, cancellationToken);
            }
        }

        return response;
    }

    private async Task<string?> GetValidTokenAsync()
    {
        var token = await authService.GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token)) return null;
        if (IsExpiringSoon(token))
            token = await RefreshAndGetTokenAsync() ?? token;
        return token;
    }

    private async Task<string?> RefreshAndGetTokenAsync()
    {
        await _refreshLock.WaitAsync();
        try
        {
            // Re-check — another concurrent caller may have already refreshed
            var current = await authService.GetAccessTokenAsync();
            if (!string.IsNullOrEmpty(current) && !IsExpiringSoon(current))
                return current;

            var result = await authService.RefreshAsync();
            if (result.Succeeded)
            {
                await authState.NotifyStateChanged();
                return await authService.GetAccessTokenAsync();
            }

            // Refresh failed — clear tokens, user will be redirected to login
            await authService.ClearTokensAsync();
            await authState.NotifyStateChanged();
            return null;
        }
        finally { _refreshLock.Release(); }
    }

    private static bool IsExpiringSoon(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length != 3) return false;
            var payload = parts[1].PadRight(parts[1].Length + (4 - parts[1].Length % 4) % 4, '=');
            var bytes = Convert.FromBase64String(payload);
            using var doc = System.Text.Json.JsonDocument.Parse(bytes);
            if (!doc.RootElement.TryGetProperty("exp", out var expEl)) return false;
            return DateTimeOffset.FromUnixTimeSeconds(expEl.GetInt64()) < DateTimeOffset.UtcNow.AddSeconds(60);
        }
        catch { return false; }
    }

    private static void SetBearer(HttpRequestMessage req, string? token)
    {
        if (!string.IsNullOrEmpty(token))
            req.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);
        foreach (var h in original.Headers)
            clone.Headers.TryAddWithoutValidation(h.Key, h.Value);
        if (original.Content != null)
        {
            var bytes = await original.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(bytes);
            foreach (var h in original.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
        }
        return clone;
    }
}
