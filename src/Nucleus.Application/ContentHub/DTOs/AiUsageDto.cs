namespace Nucleus.Application.ContentHub.DTOs;

public class AiUsageDto
{
    public Guid Id { get; set; }
    public Guid BrandId { get; set; }
    public string Feature { get; set; } = string.Empty;
    public int TokensUsed { get; set; }
    public decimal CostUsd { get; set; }
    public string Model { get; set; } = string.Empty;
    public Guid? ContentPageId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
