namespace Nucleus.Application.ContentHub.DTOs;

public class BannedWordDto
{
    public Guid Id { get; set; }
    public Guid BrandId { get; set; }
    public string Word { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
