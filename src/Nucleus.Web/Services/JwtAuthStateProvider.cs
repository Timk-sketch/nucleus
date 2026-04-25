using Microsoft.AspNetCore.Components.Authorization;

namespace Nucleus.Web.Services;

public class JwtAuthStateProvider(AuthService authService) : AuthenticationStateProvider
{
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var user = await authService.GetCurrentUserAsync();
        return new AuthenticationState(user);
    }

    public void NotifyStateChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
}
