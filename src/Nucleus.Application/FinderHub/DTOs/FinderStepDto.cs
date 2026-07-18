namespace Nucleus.Application.FinderHub.DTOs;

/// <summary>A single step within a Finder, with its options.</summary>
public class FinderStepDto
{
    public Guid Id { get; set; }
    public Guid FinderId { get; set; }
    public int StepOrder { get; set; }
    public string StepType { get; set; } = "single_choice";
    public string QuestionText { get; set; } = string.Empty;
    public string? HelperText { get; set; }
    public bool IsRequired { get; set; }
    public List<FinderOptionDto> Options { get; set; } = [];
}
