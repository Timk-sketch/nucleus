using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nucleus.Application.Auth.Commands;

namespace Nucleus.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new RegisterCommand(
            req.TenantName, req.Email, req.Password, req.FirstName, req.LastName), ct);

        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors });

        return Ok(new AuthResponse(result.AccessToken!, result.RefreshToken!, result.ExpiresIn));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new LoginCommand(req.Email, req.Password), ct);

        if (!result.Succeeded)
            return Unauthorized(new { error = result.Error });

        return Ok(new AuthResponse(result.AccessToken!, result.RefreshToken!, result.ExpiresIn));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new RefreshTokenCommand(req.RefreshToken), ct);

        if (!result.Succeeded)
            return Unauthorized(new { error = result.Error });

        return Ok(new AuthResponse(result.AccessToken!, result.RefreshToken!, result.ExpiresIn));
    }
}

public record RegisterRequest(string TenantName, string Email, string Password, string FirstName, string LastName);
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record AuthResponse(string AccessToken, string RefreshToken, int ExpiresIn);
