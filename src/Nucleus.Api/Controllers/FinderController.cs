using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nucleus.Application.FinderHub.Commands;
using Nucleus.Application.FinderHub.Queries;

namespace Nucleus.Api.Controllers;

/// <summary>
/// Finder Hub — Quiz Builder endpoints.
///
/// AUTHENTICATED (admin/builder):
///   GET  /api/finder?brandId=                      — list finders for a brand
///   POST /api/finder                                — create finder
///   GET  /api/finder/{id}/builder                  — full builder view (steps + results)
///   POST /api/finder/{id}/publish                  — publish finder
///   POST /api/finder/{id}/steps                    — add a step
///   POST /api/finder/steps/{stepId}/options        — add an option to a step
///   POST /api/finder/{id}/results                  — add a result
///   GET  /api/finder/{id}/analytics?days=30        — finder analytics
///
/// UNAUTHENTICATED (embed widget):
///   GET  /api/finder/{embedToken}                  — get public config (steps + results)
///   GET  /api/finder/{embedToken}/session/{token}  — resume session
///   POST /api/finder/{embedToken}/session          — start/update session
///   POST /api/finder/{embedToken}/convert          — record conversion
/// </summary>
[ApiController]
[Produces("application/json")]
public class FinderController(IMediator mediator) : ControllerBase
{
    // ─── Admin / Builder Endpoints (Authenticated) ────────────────────────

    /// <summary>GET /api/finder?brandId={id} — list finders for a brand</summary>
    [HttpGet("api/finder")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> GetFinders(
        [FromQuery] Guid brandId,
        CancellationToken ct = default)
    {
        if (brandId == Guid.Empty)
            return BadRequest(new { success = false, error = "brandId is required." });

        var finders = await mediator.Send(new GetFindersQuery(brandId), ct);
        return Ok(new { success = true, data = finders });
    }

    /// <summary>POST /api/finder — create a new finder</summary>
    [HttpPost("api/finder")]
    [Authorize]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreateFinder(
        [FromBody] CreateFinderRequest req,
        CancellationToken ct)
    {
        try
        {
            var id = await mediator.Send(
                new CreateFinderCommand(req.BrandId, req.Name, req.Slug, req.IntroText), ct);

            return CreatedAtAction(nameof(GetFinderBuilder),
                new { id },
                new { success = true, data = new { id } });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>GET /api/finder/{id}/builder — full builder view</summary>
    [HttpGet("api/finder/{id:guid}/builder")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetFinderBuilder(
        Guid id,
        CancellationToken ct = default)
    {
        var builder = await mediator.Send(new GetFinderBuilderQuery(id), ct);

        if (builder is null)
            return NotFound(new { success = false, error = "Finder not found." });

        return Ok(new { success = true, data = builder });
    }

    /// <summary>POST /api/finder/{id}/publish — publish a finder</summary>
    [HttpPost("api/finder/{id:guid}/publish")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> PublishFinder(
        Guid id,
        CancellationToken ct)
    {
        try
        {
            var found = await mediator.Send(new PublishFinderCommand(id), ct);

            return found
                ? Ok(new { success = true, message = "Finder published." })
                : NotFound(new { success = false, error = "Finder not found." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>POST /api/finder/{id}/steps — add a step to a finder</summary>
    [HttpPost("api/finder/{id:guid}/steps")]
    [Authorize]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> AddStep(
        Guid id,
        [FromBody] AddStepRequest req,
        CancellationToken ct)
    {
        try
        {
            var step = await mediator.Send(
                new AddFinderStepCommand(id, req.QuestionText, req.StepType ?? "single_choice",
                    req.HelperText, req.IsRequired ?? true), ct);

            return Created($"api/finder/{id}/builder",
                new { success = true, data = step });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>POST /api/finder/steps/{stepId}/options — add an option to a step</summary>
    [HttpPost("api/finder/steps/{stepId:guid}/options")]
    [Authorize]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> AddOption(
        Guid stepId,
        [FromBody] AddOptionRequest req,
        CancellationToken ct)
    {
        try
        {
            var option = await mediator.Send(
                new AddFinderOptionCommand(stepId, req.Label, req.Value, req.IconUrl, req.Description), ct);

            return Created($"api/finder/steps/{stepId}/options",
                new { success = true, data = option });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>POST /api/finder/{id}/results — add a result to a finder</summary>
    [HttpPost("api/finder/{id:guid}/results")]
    [Authorize]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> AddResult(
        Guid id,
        [FromBody] AddResultRequest req,
        CancellationToken ct)
    {
        try
        {
            var result = await mediator.Send(
                new AddFinderResultCommand(id, req.ProductKey, req.Headline,
                    req.ConditionJson ?? "{}", req.Body, req.CtaLabel, req.CtaUrl), ct);

            return Created($"api/finder/{id}/builder",
                new { success = true, data = result });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>GET /api/finder/{id}/analytics?days=30 — finder analytics</summary>
    [HttpGet("api/finder/{id:guid}/analytics")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetAnalytics(
        Guid id,
        [FromQuery] int days = 30,
        CancellationToken ct = default)
    {
        var analytics = await mediator.Send(new GetFinderAnalyticsQuery(id, days), ct);

        if (analytics is null)
            return NotFound(new { success = false, error = "Finder not found." });

        return Ok(new { success = true, data = analytics });
    }

    // ─── Public / Embed Endpoints (No Auth) ──────────────────────────────

    /// <summary>GET /api/finder/{embedToken} — public config for embed widget</summary>
    [HttpGet("api/finder/{embedToken}")]
    [AllowAnonymous]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetPublicFinder(
        string embedToken,
        CancellationToken ct = default)
    {
        // Guard: prevent GUID-shaped tokens from being confused with the {id:guid} routes
        if (Guid.TryParse(embedToken, out _))
            return BadRequest(new { success = false, error = "Use /api/finder/{guid}/builder for authenticated access." });

        var finder = await mediator.Send(new GetPublicFinderQuery(embedToken), ct);

        if (finder is null)
            return NotFound(new { success = false, error = "Finder not found or not published." });

        return Ok(new { success = true, data = finder });
    }

    /// <summary>GET /api/finder/{embedToken}/session/{sessionToken} — resume session</summary>
    [HttpGet("api/finder/{embedToken}/session/{sessionToken}")]
    [AllowAnonymous]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetSession(
        string embedToken,
        string sessionToken,
        CancellationToken ct = default)
    {
        var session = await mediator.Send(new GetFinderSessionQuery(embedToken, sessionToken), ct);

        if (session is null)
            return NotFound(new { success = false, error = "Session not found." });

        return Ok(new { success = true, data = session });
    }

    /// <summary>POST /api/finder/{embedToken}/session — start or update a session</summary>
    [HttpPost("api/finder/{embedToken}/session")]
    [AllowAnonymous]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RecordSession(
        string embedToken,
        [FromBody] RecordSessionRequest req,
        CancellationToken ct)
    {
        try
        {
            var session = await mediator.Send(
                new RecordFinderSessionCommand(
                    embedToken,
                    req.AnswersJson,
                    req.SessionToken,
                    req.IsComplete ?? false), ct);

            return Ok(new { success = true, data = session });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>POST /api/finder/{embedToken}/convert — record CTA conversion</summary>
    [HttpPost("api/finder/{embedToken}/convert")]
    [AllowAnonymous]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RecordConversion(
        string embedToken,
        [FromBody] ConvertRequest req,
        CancellationToken ct)
    {
        try
        {
            var found = await mediator.Send(
                new RecordFinderConversionCommand(embedToken, req.SessionToken), ct);

            return found
                ? Ok(new { success = true, message = "Conversion recorded." })
                : NotFound(new { success = false, error = "Session not found." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }
}

// ─── Request models ───────────────────────────────────────────────────────────

public record CreateFinderRequest(
    Guid BrandId,
    string Name,
    string Slug,
    string? IntroText = null);

public record AddStepRequest(
    string QuestionText,
    string? StepType = null,
    string? HelperText = null,
    bool? IsRequired = null);

public record AddOptionRequest(
    string Label,
    string Value,
    string? IconUrl = null,
    string? Description = null);

public record AddResultRequest(
    string ProductKey,
    string Headline,
    string? ConditionJson = null,
    string? Body = null,
    string? CtaLabel = null,
    string? CtaUrl = null);

public record RecordSessionRequest(
    string AnswersJson,
    string? SessionToken = null,
    bool? IsComplete = null);

public record ConvertRequest(string SessionToken);
