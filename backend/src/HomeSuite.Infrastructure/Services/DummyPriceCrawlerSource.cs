using HomeSuite.Application.Interfaces;

namespace HomeSuite.Infrastructure.Services;

public class DummyPriceCrawlerSource : IPriceCrawlerSource
{
    public Task<List<CrawledCatalogPriceResult>> SearchAsync(
        CrawledCatalogPriceRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<CrawledCatalogPriceResult>());
    }
}
