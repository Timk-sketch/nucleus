namespace Nucleus.Application.FinderHub.DTOs;

/// <summary>A selectable option within a FinderStep.</summary>
public class FinderOptionDto
{
    public Guid Id { get; set; }
    public Guid StepId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
}
