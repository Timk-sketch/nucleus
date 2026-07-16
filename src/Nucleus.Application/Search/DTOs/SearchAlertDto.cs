namespace Nucleus.Application.Search.DTOs;

public class SearchAlertDto
{
    public Guid Id { get; set; }
    public Guid KeywordId { get; set; }
    public string Keyword { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty;
    public int Threshold { get; set; }
    public bool IsActive { get; set; }
    public string? Message { get; set; }
    public DateTimeOffset? TriggeredAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
