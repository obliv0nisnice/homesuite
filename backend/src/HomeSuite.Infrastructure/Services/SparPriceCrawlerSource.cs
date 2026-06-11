using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using HomeSuite.Application.Interfaces;

namespace HomeSuite.Infrastructure.Services;

public class SparPriceCrawlerSource : HtmlSearchPriceCrawlerSourceBase
{
    // "11.96 €/kg" → 11.96 (Dezimalpunkt, anders als die de-AT-Preise im HTML)
    private static readonly Regex PricePerUnitRegex = new(
        @"(\d+(?:\.\d+)?)",
        RegexOptions.Compiled);

    public SparPriceCrawlerSource(
        HttpClient httpClient,
        ILogger<SparPriceCrawlerSource> logger,
        IPlaywrightStoreSearchService? playwrightSearchService = null)
        : base(httpClient, logger, playwrightSearchService)
    {
    }

    protected override string StoreName => "SPAR";
    protected override string StoreDomain => "spar.at";
    protected override string SourceType => "crawler:spar";

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
            Logger.LogWarning(ex, "SPAR-API-Suche fehlgeschlagen für Query '{Query}', Fallback auf Websuche.", request.Query);
        }

        return await base.SearchAsync(request, cancellationToken);
    }

    // Offizielle Produktsuche des SPAR-Onlineshops (FactFinder). Die Produktseiten
    // auf spar.at liefern Nicht-Browsern nur 403, die Such-API antwortet mit JSON
    // inklusive Preis — eine Seiten-Crawl-Stufe ist daher weder nötig noch möglich.
    private async Task<List<CrawledCatalogPriceResult>> SearchViaApiAsync(
        CrawledCatalogPriceRequest request,
        CancellationToken cancellationToken)
    {
        var query = $"{request.Query} {request.BrandHint}".Trim();
        var url = "https://search-spar.spar-ics.com/fact-finder/rest/v5/search/products_lmos_at" +
                  $"?query={Uri.EscapeDataString(query)}&hitsPerPage=12";

        using var response = await HttpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            Logger.LogWarning("SPAR-API-Request fehlgeschlagen: {StatusCode}", response.StatusCode);
            return [];
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!json.RootElement.TryGetProperty("hits", out var hits) || hits.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var queryTokens = BuildTokens(request.Query, null);
        var brandTokens = BuildTokens(request.BrandHint ?? string.Empty, null);
        var results = new List<(int Score, CrawledCatalogPriceResult Result)>();

        foreach (var hit in hits.EnumerateArray())
        {
            if (!hit.TryGetProperty("masterValues", out var values) || values.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var name = GetString(values, "name");
            var price = GetDecimal(values, "best-price") ?? GetDecimal(values, "price");
            if (string.IsNullOrWhiteSpace(name) || price is null)
            {
                continue;
            }

            var productName = NormalizeWhitespace(name);
            var relativeUrl = GetString(values, "url");

            // SPAR kürzt Namen ab ("Pamp. Baby Dry") — der URL-Slug enthält den
            // vollen Produktnamen und muss daher mit in die Relevanz-Bewertung.
            var score = ScoreProduct($"{productName} {relativeUrl}", queryTokens, brandTokens);
            if (score <= 0)
            {
                continue;
            }
            results.Add((score, new CrawledCatalogPriceResult
            {
                StoreName = StoreName,
                ProductName = productName,
                UnitPrice = ParsePricePerUnit(GetString(values, "price-per-unit")),
                TotalPrice = price,
                ProductUrl = string.IsNullOrWhiteSpace(relativeUrl) ? null : $"https://www.interspar.at{relativeUrl}",
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

    private static string? GetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static decimal? GetDecimal(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDecimal()
            : null;
    }

    private static decimal? ParsePricePerUnit(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = PricePerUnitRegex.Match(value);
        return match.Success && decimal.TryParse(match.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }
}
