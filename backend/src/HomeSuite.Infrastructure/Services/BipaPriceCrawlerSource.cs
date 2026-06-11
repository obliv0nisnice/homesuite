using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using HomeSuite.Application.Interfaces;

namespace HomeSuite.Infrastructure.Services;

public class BipaPriceCrawlerSource : HtmlSearchPriceCrawlerSourceBase
{
    public BipaPriceCrawlerSource(
        HttpClient httpClient,
        ILogger<BipaPriceCrawlerSource> logger,
        IPlaywrightStoreSearchService? playwrightSearchService = null)
        : base(httpClient, logger, playwrightSearchService)
    {
    }

    protected override string StoreName => "BIPA";
    protected override string StoreDomain => "bipa.at";
    protected override string SourceType => "crawler:bipa";

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
            var directUrls = await SearchDirectProductUrlsAsync(request, cancellationToken);
            if (directUrls.Count > 0)
            {
                return await BuildRankedResultsAsync(directUrls, request, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "BIPA-Direktsuche fehlgeschlagen für Query '{Query}', Fallback auf Websuche.", request.Query);
        }

        return await base.SearchAsync(request, cancellationToken);
    }

    // Die BIPA-Suchseite ist serverseitig gerendert und enthält die /p/-Produktlinks
    // direkt im HTML — kein Browser nötig.
    private async Task<List<string>> SearchDirectProductUrlsAsync(
        CrawledCatalogPriceRequest request,
        CancellationToken cancellationToken)
    {
        var searchUrl = $"https://www.bipa.at/suche?q={Uri.EscapeDataString(request.Query.Trim())}";

        using var response = await HttpClient.GetAsync(searchUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            Logger.LogWarning("BIPA-Suchrequest fehlgeschlagen: {StatusCode}", response.StatusCode);
            return [];
        }

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(html))
        {
            return [];
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var anchors = doc.DocumentNode.SelectNodes("//a[@href]");
        if (anchors is null || anchors.Count == 0)
        {
            return [];
        }

        return anchors
            .Select(a => a.GetAttributeValue("href", string.Empty))
            .Where(href => href.Contains("/p/", StringComparison.OrdinalIgnoreCase))
            .Select(href => href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? href
                : $"https://www.bipa.at{href}")
            .Where(IsStoreUrl)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();
    }
}
