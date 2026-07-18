namespace Nucleus.Application.FinderHub.DTOs;

/// <summary>
/// Full Finder config returned by the unauthenticated public endpoint.
/// Used by the embeddable widget to render steps, options, and results client-side.
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
}
