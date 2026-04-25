namespace Nucleus.Web.Services;

/// <summary>
/// Delegating handler that attaches the stored JWT Bearer token to every outgoing request.
/// </summary>
public class AuthHeaderHandler(AuthService authService) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await authService.GetAccessTokenAsync();
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken);
    }
}
