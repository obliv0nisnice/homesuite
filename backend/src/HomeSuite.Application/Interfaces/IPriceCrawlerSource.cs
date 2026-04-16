namespace HomeSuite.Application.Interfaces;

public interface IPriceCrawlerSource
{
    Task<List<CrawledCatalogPriceResult>> SearchAsync(
        CrawledCatalogPriceRequest request,
        CancellationToken cancellationToken = default);
}

public class CrawledCatalogPriceRequest
{
    public string Query { get; set; } = string.Empty;
    public string? BrandHint { get; set; }
    public string? Unit { get; set; }
}

public class CrawledCatalogPriceResult
{
    public string StoreName { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal? UnitPrice { get; set; }
    public decimal? TotalPrice { get; set; }
    public string? ProductUrl { get; set; }
    public bool IsAvailable { get; set; } = true;
    public string SourceType { get; set; } = "crawler";
}
