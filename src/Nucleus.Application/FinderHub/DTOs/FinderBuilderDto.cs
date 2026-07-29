using Nucleus.Application.FinderHub.Commands;

namespace Nucleus.Application.FinderHub.DTOs;

/// <summary>
/// Full builder view of a Finder — returned by GetFinderBuilderQuery.
/// Contains all steps (with options), all results, and A/B variants for the admin builder UI.
/// </summary>
public class FinderBuilderDto
{
    public Guid Id { get; set; }
    public Guid BrandId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? IntroText { get; set; }
    public string Status { get; set; } = "draft";
    public DateTimeOffset? PublishedAt { get; set; }
    public string EmbedToken { get; set; } = string.Empty;
    public List<FinderStepDto> Steps { get; set; } = [];
    public List<FinderResultDto> Results { get; set; } = [];
    public List<FinderVariantDto> Variants { get; set; } = [];

    // White-label settings (agency plan)
    public bool WhiteLabelEnabled { get; set; }
    public string? CustomCss { get; set; }
    public string? LogoUrl { get; set; }
    public string? PrimaryColorOverride { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
