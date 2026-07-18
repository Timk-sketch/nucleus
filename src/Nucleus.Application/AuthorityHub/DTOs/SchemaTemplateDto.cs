namespace Nucleus.Application.AuthorityHub.DTOs;

public class SchemaTemplateDto
{
    public Guid Id { get; set; }
    public Guid BrandId { get; set; }
    public string PageType { get; set; } = string.Empty;
    public string SchemaType { get; set; } = string.Empty;
    public string TemplateJson { get; set; } = "{}";
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
