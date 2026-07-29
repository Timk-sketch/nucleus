using Nucleus.Application.FinderHub.Commands;

namespace Nucleus.Application.FinderHub.DTOs;

/// <summary>
/// Full Finder config returned by the unauthenticated public endpoint.
/// Used by the embeddable widget to render steps, options, and results client-side.
/// White-label fields are only populated when WhiteLabelEnabled = true (agency plan).
/// </summary>
public class PublicFinderDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? IntroText { get; set; }
    public string EmbedToken { get; set; } = string.Empty;
    public List<FinderStepDto> Steps { get; set; } = [];
    public List<FinderResultDto> Results { get; set; } = [];

    // White-label embed (agency plan)
    public bool WhiteLabelEnabled { get; set; }
    public string? CustomCss { get; set; }
    public string? LogoUrl { get; set; }
    public string? PrimaryColorOverride { get; set; }

    /// <summary>A/B variant assigned to this session (null if no active variants).</summary>
    public FinderVariantDto? AssignedVariant { get; set; }
}
