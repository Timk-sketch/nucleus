namespace Nucleus.Application.ReportsHub.DTOs;

public class FinderReportDto
{
    public int DaysWindow { get; set; }
    public List<FinderSummaryRow> Finders { get; set; } = [];
}

public class FinderSummaryRow
{
    public Guid FinderId { get; set; }
    public string FinderName { get; set; } = "";
    public string Status { get; set; } = "";
    public int Starts { get; set; }
    public int Completions { get; set; }
    public int Conversions { get; set; }
    public int LeadsCaptured { get; set; }
    public double? CompletionRate { get; set; }
    public double? ConversionRate { get; set; }
}
