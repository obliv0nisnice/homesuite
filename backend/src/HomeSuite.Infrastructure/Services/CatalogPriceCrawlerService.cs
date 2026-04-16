using HomeSuite.Application.Interfaces;
using HomeSuite.Domain.Entities;
using HomeSuite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HomeSuite.Infrastructure.Services;

public class CatalogPriceCrawlerService : ICatalogPriceCrawlerService
{
    private readonly HomeSuiteDbContext _dbContext;
    private readonly IEnumerable<IPriceCrawlerSource> _sources;
    private readonly ILogger<CatalogPriceCrawlerService> _logger;

    public CatalogPriceCrawlerService(
        HomeSuiteDbContext dbContext,
        IEnumerable<IPriceCrawlerSource> sources,
        ILogger<CatalogPriceCrawlerService> logger)
    {
        _dbContext = dbContext;
        _sources = sources;
        _logger = logger;
    }

    public async Task RefreshAllCatalogPricesAsync(CancellationToken cancellationToken = default)
    {
        var catalogItemIds = await _dbContext.CatalogItems
            .OrderBy(x => x.Name)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (var catalogItemId in catalogItemIds)
        {
            try
            {
                await RefreshCatalogItemPricesAsync(catalogItemId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Preisrefresh für CatalogItem {CatalogItemId}", catalogItemId);
            }
        }
    }

    public async Task RefreshCatalogItemPricesAsync(Guid catalogItemId, CancellationToken cancellationToken = default)
{
    var item = await _dbContext.CatalogItems
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.Id == catalogItemId, cancellationToken);

    if (item is null)
    {
        throw new InvalidOperationException("CatalogItem nicht gefunden.");
    }

    var request = new CrawledCatalogPriceRequest
    {
        Query = string.IsNullOrWhiteSpace(item.SearchTerm) ? item.Name : item.SearchTerm!,
        BrandHint = item.BrandHint,
        Unit = item.DefaultUnit
    };

    var crawledResults = new List<CrawledCatalogPriceResult>();

    foreach (var source in _sources)
    {
        try
        {
            var results = await source.SearchAsync(request, cancellationToken);
            crawledResults.AddRange(results);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Crawler-Quelle fehlgeschlagen für CatalogItem {CatalogItemId}", catalogItemId);
        }
    }

    crawledResults = crawledResults
        .Where(x => !string.IsNullOrWhiteSpace(x.StoreName))
        .Where(x => !string.IsNullOrWhiteSpace(x.ProductName))
        .GroupBy(x => new
        {
            Store = x.StoreName.Trim().ToLowerInvariant(),
            Product = x.ProductName.Trim().ToLowerInvariant(),
            Url = (x.ProductUrl ?? string.Empty).Trim().ToLowerInvariant(),
            TotalPrice = x.TotalPrice,
            UnitPrice = x.UnitPrice,
            SourceType = (x.SourceType ?? string.Empty).Trim().ToLowerInvariant()
        })
        .Select(x => x.First())
        .ToList();

    if (crawledResults.Count == 0)
    {
        _logger.LogWarning(
            "Keine neuen Preise für CatalogItem {CatalogItemId} gefunden. Bestehende Preise bleiben erhalten.",
            catalogItemId);
        return;
    }

    var sourceTypesToReplace = crawledResults
        .Select(x => x.SourceType?.Trim())
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    var existingPrices = await _dbContext.CatalogItemPrices
        .Where(x => x.CatalogItemId == catalogItemId)
        .Where(x => sourceTypesToReplace.Contains(x.SourceType))
        .ToListAsync(cancellationToken);

    if (existingPrices.Count > 0)
    {
        _dbContext.CatalogItemPrices.RemoveRange(existingPrices);
    }

    var newPrices = crawledResults.Select(result => new CatalogItemPrice
    {
        Id = Guid.NewGuid(),
        CatalogItemId = catalogItemId,
        StoreName = result.StoreName.Trim(),
        ProductName = result.ProductName.Trim(),
        UnitPrice = result.UnitPrice,
        TotalPrice = result.TotalPrice,
        ProductUrl = string.IsNullOrWhiteSpace(result.ProductUrl) ? null : result.ProductUrl.Trim(),
        IsAvailable = result.IsAvailable,
        CheckedAt = DateTime.UtcNow,
        SourceType = string.IsNullOrWhiteSpace(result.SourceType) ? "crawler" : result.SourceType.Trim()
    }).ToList();

    await _dbContext.CatalogItemPrices.AddRangeAsync(newPrices, cancellationToken);
    await _dbContext.SaveChangesAsync(cancellationToken);
}
}
