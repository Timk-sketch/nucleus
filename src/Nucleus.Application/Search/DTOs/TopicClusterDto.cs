namespace Nucleus.Application.Search.DTOs;

public class TopicClusterDto
{
    public Guid Id { get; set; }
    public Guid BrandId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PillarKeyword { get; set; } = string.Empty;
    public List<string> ClusterKeywords { get; set; } = [];
    public string Status { get; set; } = "planning";
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
