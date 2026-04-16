namespace HomeSuite.Application.Interfaces;

public interface ICatalogPriceCrawlerService
{
    Task RefreshAllCatalogPricesAsync(CancellationToken cancellationToken = default);
    Task RefreshCatalogItemPricesAsync(Guid catalogItemId, CancellationToken cancellationToken = default);
}
