using System.Text.Json;
using Microsoft.Extensions.Logging;
using HomeSuite.Application.Interfaces;

namespace HomeSuite.Infrastructure.Services;

public class DmPriceCrawlerSource : HtmlSearchPriceCrawlerSourceBase
{
    public DmPriceCrawlerSource(
        HttpClient httpClient,
        ILogger<DmPriceCrawlerSource> logger,
        IPlaywrightStoreSearchService? playwrightSearchService = null)
        : base(httpClient, logger, playwrightSearchService)
    {
    }

    protected override string StoreName => "dm";
    protected override string StoreDomain => "dm.at";
    protected override string SourceType => "crawler:dm";

    public override async Task<List<CrawledCatalogPriceResult>> SearchAsync(
        CrawledCatalogPriceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return [];
        }

        try
        {
            var apiResults = await SearchViaApiAsync(request, cancellationToken);
            if (apiResults.Count > 0)
            {
                return apiResults;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "dm-API-Suche fehlgeschlagen für Query '{Query}', Fallback auf Websuche.", request.Query);
        }

        return await base.SearchAsync(request, cancellationToken);
    }

    // Offizielle Produktsuche des dm-Shops. Die Produktseiten auf dm.at sind eine
    // SPA-Shell ohne serverseitigen Preis — nur die Such-API liefert Preisdaten.
    private async Task<List<CrawledCatalogPriceResult>> SearchViaApiAsync(
        CrawledCatalogPriceRequest request,
        CancellationToken cancellationToken)
    {
        var query = $"{request.Query} {request.BrandHint}".Trim();
        var url = "https://product-search.services.dmtech.com/at/search/crawl" +
                  $"?query={Uri.EscapeDataString(query)}&pageSize=24";

        using var response = await HttpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            Logger.LogWarning("dm-API-Request fehlgeschlagen: {StatusCode}", response.StatusCode);
            return [];
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!json.RootElement.TryGetProperty("products", out var products) || products.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var queryTokens = BuildTokens(request.Query, null);
        var brandTokens = BuildTokens(request.BrandHint ?? string.Empty, null);
        var results = new List<(int Score, CrawledCatalogPriceResult Result)>();

        foreach (var product in products.EnumerateArray())
        {
            var title = GetString(product, "title");
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var brand = GetString(product, "brandName");
            var productName = NormalizeWhitespace($"{brand} {title}");

            var hasTileData = product.TryGetProperty("tileData", out var tileData);

            // trackingData fehlt bei manchen Produkten — tileData hat den Preis immer,
            // allerdings nur als Anzeige-String ("59,90 €").
            var price = GetTrackingPrice(product)
                ?? (hasTileData ? GetTilePrice(tileData) : null);
            if (price is null)
            {
                continue;
            }

            var score = ScoreProduct(productName, queryTokens, brandTokens);
            if (score <= 0)
            {
                continue;
            }

            var relativeUrl = hasTileData ? GetString(tileData, "self") : null;

            results.Add((score, new CrawledCatalogPriceResult
            {
                StoreName = StoreName,
                ProductName = productName,
                UnitPrice = null,
                TotalPrice = price,
                ProductUrl = string.IsNullOrWhiteSpace(relativeUrl) ? null : $"https://www.dm.at{relativeUrl}",
                IsAvailable = true,
                SourceType = SourceType
            }));
        }

        return results
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Result.TotalPrice ?? decimal.MaxValue)
            .Select(x => x.Result)
            .Take(3)
            .ToList();
    }

    private static decimal? GetTrackingPrice(JsonElement product)
    {
        return product.TryGetProperty("trackingData", out var tracking)
            && tracking.ValueKind == JsonValueKind.Object
            && tracking.TryGetProperty("price", out var price)
            && price.ValueKind == JsonValueKind.Number
                ? price.GetDecimal()
                : null;
    }

    // tileData.price.price.current.value: "59,90 €" (mit geschütztem Leerzeichen)
    private static decimal? GetTilePrice(JsonElement tileData)
    {
        if (!tileData.TryGetProperty("price", out var outer) || outer.ValueKind != JsonValueKind.Object
            || !outer.TryGetProperty("price", out var inner) || inner.ValueKind != JsonValueKind.Object
            || !inner.TryGetProperty("current", out var current) || current.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var value = GetString(current, "value");
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = new string(value.Where(c => char.IsDigit(c) || c == ',').ToArray());
        return ParseDecimal(digits);
    }

    private static string? GetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
