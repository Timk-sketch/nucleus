namespace Nucleus.Application.FinderHub.DTOs;

/// <summary>DTO for a FinderSession returned after creation or lookup.</summary>
public class FinderSessionDto
{
    public Guid Id { get; set; }
    public Guid FinderId { get; set; }
    public string SessionToken { get; set; } = string.Empty;
    public string AnswersJson { get; set; } = "{}";
    public string? ResultKey { get; set; }
    public bool Converted { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
