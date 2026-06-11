namespace HomeSuite.Application.DTOs.Catalog;

public class CatalogItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DefaultUnit { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? SearchTerm { get; set; }
    public string? BrandHint { get; set; }
    public bool IsStaple { get; set; }

    public List<CatalogItemPriceDto> Prices { get; set; } = [];
    public decimal? BestUnitPrice { get; set; }
    public decimal? BestTotalPrice { get; set; }

    public decimal? AverageBestTotalPrice30d { get; set; }
    public decimal? PriceTrendPercent { get; set; }
}
