using HomeSuite.Application.DTOs.Catalog;
using HomeSuite.Application.Interfaces;
using HomeSuite.Domain.Entities;
using HomeSuite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HomeSuite.Infrastructure.Services;

public class CatalogService : ICatalogService
{
    private readonly HomeSuiteDbContext _dbContext;
    private readonly ICatalogPriceCrawlerService _catalogPriceCrawlerService;

    public CatalogService(
        HomeSuiteDbContext dbContext,
        ICatalogPriceCrawlerService catalogPriceCrawlerService)
    {
        _dbContext = dbContext;
        _catalogPriceCrawlerService = catalogPriceCrawlerService;
    }

    public async Task<List<CatalogItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await _dbContext.CatalogItems
            .Include(x => x.Prices)
            .OrderBy(x => x.Name)
            .Select(MapExpression())
            .ToListAsync(cancellationToken);

        await ApplyPriceTrendsAsync(items, cancellationToken);

        return items;
    }

    public async Task<CatalogItemDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.CatalogItems
            .Include(x => x.Prices)
            .Where(x => x.Id == id)
            .Select(MapExpression())
            .FirstOrDefaultAsync(cancellationToken);

        if (item is not null)
        {
            await ApplyPriceTrendsAsync([item], cancellationToken);
        }

        return item;
    }

    private async Task ApplyPriceTrendsAsync(IReadOnlyList<CatalogItemDto> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        var itemIds = items.Select(x => x.Id).ToList();
        var cutoff = DateTime.UtcNow.AddDays(-30);

        var averages = await _dbContext.CatalogItemPriceSnapshots
            .Where(x => itemIds.Contains(x.CatalogItemId))
            .Where(x => x.RecordedAt >= cutoff)
            .Where(x => x.BestTotalPrice.HasValue)
            .GroupBy(x => x.CatalogItemId)
            .Select(g => new { CatalogItemId = g.Key, Average = g.Average(x => x.BestTotalPrice) })
            .ToDictionaryAsync(x => x.CatalogItemId, x => x.Average, cancellationToken);

        foreach (var item in items)
        {
            if (!averages.TryGetValue(item.Id, out var average) || average is null or <= 0 || !item.BestTotalPrice.HasValue)
            {
                continue;
            }

            item.AverageBestTotalPrice30d = decimal.Round(average.Value, 2);
            item.PriceTrendPercent = decimal.Round((item.BestTotalPrice.Value - average.Value) / average.Value * 100, 1);
        }
    }

    public async Task<CatalogItemDto> CreateAsync(CreateCatalogItemRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request.Name, request.DefaultUnit);

        var item = new CatalogItem
        {
            Name = request.Name.Trim(),
            DefaultUnit = request.DefaultUnit.Trim(),
            Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim(),
            SearchTerm = string.IsNullOrWhiteSpace(request.SearchTerm) ? null : request.SearchTerm.Trim(),
            BrandHint = string.IsNullOrWhiteSpace(request.BrandHint) ? null : request.BrandHint.Trim(),
            IsStaple = request.IsStaple
        };

        _dbContext.CatalogItems.Add(item);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(item);
    }

    public async Task<CatalogItemDto?> UpdateAsync(Guid id, UpdateCatalogItemRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request.Name, request.DefaultUnit);

        var item = await _dbContext.CatalogItems
            .Include(x => x.Prices)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (item is null)
        {
            return null;
        }

        item.Name = request.Name.Trim();
        item.DefaultUnit = request.DefaultUnit.Trim();
        item.Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim();
        item.SearchTerm = string.IsNullOrWhiteSpace(request.SearchTerm) ? null : request.SearchTerm.Trim();
        item.BrandHint = string.IsNullOrWhiteSpace(request.BrandHint) ? null : request.BrandHint.Trim();
        item.IsStaple = request.IsStaple;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(item);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.CatalogItems
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (item is null)
        {
            return false;
        }

        _dbContext.CatalogItems.Remove(item);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task RefreshPricesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _catalogPriceCrawlerService.RefreshCatalogItemPricesAsync(id, cancellationToken);
    }

    public async Task RefreshAllPricesAsync(CancellationToken cancellationToken = default)
    {
        await _catalogPriceCrawlerService.RefreshAllCatalogPricesAsync(cancellationToken);
    }

    private static void ValidateRequest(string name, string defaultUnit)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Der Name ist erforderlich.");
        }

        if (string.IsNullOrWhiteSpace(defaultUnit))
        {
            throw new ArgumentException("Die Standard-Einheit ist erforderlich.");
        }
    }

    private static CatalogItemDto Map(CatalogItem item)
    {
        return new CatalogItemDto
        {
            Id = item.Id,
            Name = item.Name,
            DefaultUnit = item.DefaultUnit,
            Category = item.Category,
            SearchTerm = item.SearchTerm,
            BrandHint = item.BrandHint,
            IsStaple = item.IsStaple,
            Prices = item.Prices
                .Where(x => x.IsAvailable)
                .OrderBy(x => x.TotalPrice ?? decimal.MaxValue)
                .ThenBy(x => x.UnitPrice ?? decimal.MaxValue)
                .Select(x => new CatalogItemPriceDto
                {
                    Id = x.Id,
                    StoreName = x.StoreName,
                    ProductName = x.ProductName,
                    UnitPrice = x.UnitPrice,
                    TotalPrice = x.TotalPrice,
                    ProductUrl = x.ProductUrl,
                    IsAvailable = x.IsAvailable,
                    CheckedAt = x.CheckedAt,
                    SourceType = x.SourceType
                })
                .ToList(),
            BestUnitPrice = item.Prices
                .Where(x => x.IsAvailable)
                .Where(x => x.UnitPrice.HasValue)
                .OrderBy(x => x.UnitPrice)
                .Select(x => x.UnitPrice)
                .FirstOrDefault(),
            BestTotalPrice = item.Prices
                .Where(x => x.IsAvailable)
                .Where(x => x.TotalPrice.HasValue)
                .OrderBy(x => x.TotalPrice)
                .Select(x => x.TotalPrice)
                .FirstOrDefault()
        };
    }

    private static System.Linq.Expressions.Expression<Func<CatalogItem, CatalogItemDto>> MapExpression()
    {
        return item => new CatalogItemDto
        {
            Id = item.Id,
            Name = item.Name,
            DefaultUnit = item.DefaultUnit,
            Category = item.Category,
            SearchTerm = item.SearchTerm,
            BrandHint = item.BrandHint,
            IsStaple = item.IsStaple,
            Prices = item.Prices
                .Where(x => x.IsAvailable)
                .OrderBy(x => x.TotalPrice ?? decimal.MaxValue)
                .ThenBy(x => x.UnitPrice ?? decimal.MaxValue)
                .Select(x => new CatalogItemPriceDto
                {
                    Id = x.Id,
                    StoreName = x.StoreName,
                    ProductName = x.ProductName,
                    UnitPrice = x.UnitPrice,
                    TotalPrice = x.TotalPrice,
                    ProductUrl = x.ProductUrl,
                    IsAvailable = x.IsAvailable,
                    CheckedAt = x.CheckedAt,
                    SourceType = x.SourceType
                })
                .ToList(),
            BestUnitPrice = item.Prices
                .Where(x => x.IsAvailable)
                .Where(x => x.UnitPrice.HasValue)
                .OrderBy(x => x.UnitPrice)
                .Select(x => x.UnitPrice)
                .FirstOrDefault(),
            BestTotalPrice = item.Prices
                .Where(x => x.IsAvailable)
                .Where(x => x.TotalPrice.HasValue)
                .OrderBy(x => x.TotalPrice)
                .Select(x => x.TotalPrice)
                .FirstOrDefault()
        };
    }
}
