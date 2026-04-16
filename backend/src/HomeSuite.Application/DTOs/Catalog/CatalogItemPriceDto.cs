namespace HomeSuite.Application.DTOs.Catalog;

public class CatalogItemPriceDto
{
    public Guid Id { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal? UnitPrice { get; set; }
    public decimal? TotalPrice { get; set; }
    public string? ProductUrl { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime CheckedAt { get; set; }
    public string SourceType { get; set; } = string.Empty;
}
