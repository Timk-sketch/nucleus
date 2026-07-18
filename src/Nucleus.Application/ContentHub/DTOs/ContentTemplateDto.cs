namespace Nucleus.Application.ContentHub.DTOs;

public class ContentTemplateDto
{
    public Guid Id { get; set; }
    public Guid BrandId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PageType { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsGlobal { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
