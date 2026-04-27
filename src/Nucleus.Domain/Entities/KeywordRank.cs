namespace Nucleus.Domain.Entities;

public class KeywordRank : TenantEntity
{
    public Guid KeywordId { get; set; }
    public BrandKeyword Keyword { get; set; } = null!;
    public Guid BrandId { get; set; }
    public int? Position { get; set; }
    public int? PreviousPosition { get; set; }
    public int? SearchVolume { get; set; }
    public int? KeywordDifficulty { get; set; }
    public string? RankedUrl { get; set; }
    public DateTimeOffset CheckedAt { get; set; } = DateTimeOffset.UtcNow;
}
