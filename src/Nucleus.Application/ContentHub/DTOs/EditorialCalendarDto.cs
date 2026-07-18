namespace Nucleus.Application.ContentHub.DTOs;

public class EditorialCalendarDto
{
    public Guid BrandId { get; set; }
    public DateTimeOffset WindowStart { get; set; }
    public DateTimeOffset WindowEnd { get; set; }
    public List<CalendarEntryDto> Entries { get; set; } = [];
}

public class CalendarEntryDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string PageType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? KeywordText { get; set; }
    public DateTimeOffset? ScheduledAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
