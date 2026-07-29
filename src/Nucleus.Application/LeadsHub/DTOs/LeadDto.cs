namespace Nucleus.Application.LeadsHub.DTOs;

public class LeadDto
{
    public Guid SessionId { get; set; }
    public Guid FinderId { get; set; }
    public string FinderName { get; set; } = "";
    public string? LeadName { get; set; }
    public string? LeadEmail { get; set; }
    public string? LeadPhone { get; set; }
    public string AnswersJson { get; set; } = "{}";
    public bool Converted { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class LeadsPageDto
{
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<LeadDto> Items { get; set; } = [];
}
