namespace Nucleus.Application.FinderHub.DTOs;

/// <summary>
/// Full builder view of a Finder — returned by GetFinderBuilderQuery.
/// Contains all steps (with options) and all results for the admin builder UI.
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
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
