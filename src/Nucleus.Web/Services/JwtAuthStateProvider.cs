using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace Nucleus.Web.Services;

public class JwtAuthStateProvider(AuthService authService) : AuthenticationStateProvider
{
    private AuthenticationState _current = new(new ClaimsPrincipal(new ClaimsIdentity()));

    // Synchronous — returns cached state immediately (no JS interop on render thread)
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => Task.FromResult(_current);

    // Called from App.razor OnAfterRenderAsync and after login/logout
    public async Task InitializeAsync()
    {
        var user = await authService.GetCurrentUserAsync();
        _current = new AuthenticationState(user);
        NotifyAuthenticationStateChanged(Task.FromResult(_current));
    }

    public Task NotifyStateChanged() => InitializeAsync();
}
