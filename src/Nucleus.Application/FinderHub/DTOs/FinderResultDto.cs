namespace Nucleus.Application.FinderHub.DTOs;

/// <summary>A result configuration returned when a user's answers match the condition.</summary>
public class FinderResultDto
{
    public Guid Id { get; set; }
    public Guid FinderId { get; set; }
    public string ConditionJson { get; set; } = "{}";
    public string ProductKey { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string? CtaLabel { get; set; }
    public string? CtaUrl { get; set; }
}
