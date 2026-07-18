namespace Nucleus.Application.ContentHub.DTOs;

public class BrandVoiceDto
{
    public Guid BrandId { get; set; }
    public string BrandName { get; set; } = string.Empty;
    public List<BannedWordDto> BannedWords { get; set; } = [];
    public int TotalBannedWords { get; set; }
}
