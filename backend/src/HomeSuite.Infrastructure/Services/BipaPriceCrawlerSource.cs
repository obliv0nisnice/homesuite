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
            var searchUrl = $"https://www.bipa.at/suche?q={Uri.EscapeDataString(request.Query.Trim())}";
            var directUrls = await SearchProductUrlsWithPlaywrightAsync(
                searchUrl,
                ["/p/"],
                url => url.Contains("/p/", StringComparison.OrdinalIgnoreCase),
                cancellationToken);

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
            Logger.LogWarning(ex, "BIPA-Direktsuche via Playwright fehlgeschlagen für Query '{Query}', Fallback auf Websuche.", request.Query);
        }

        return await base.SearchAsync(request, cancellationToken);
    }
}
