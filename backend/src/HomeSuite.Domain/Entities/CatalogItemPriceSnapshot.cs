namespace HomeSuite.Domain.Entities;

public class CatalogItemPriceSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CatalogItemId { get; set; }
    public CatalogItem? CatalogItem { get; set; }

    public string StoreName { get; set; } = string.Empty;
    public decimal? BestTotalPrice { get; set; }
    public decimal? BestUnitPrice { get; set; }

    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
