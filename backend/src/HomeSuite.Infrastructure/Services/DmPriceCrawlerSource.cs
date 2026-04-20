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
            var searchUrl = $"https://www.dm.at/search?query={Uri.EscapeDataString(request.Query.Trim())}";
            var directUrls = await SearchProductUrlsWithPlaywrightAsync(
                searchUrl,
                ["-p", ".html"],
                url => url.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
                    && url.Contains("-p", StringComparison.OrdinalIgnoreCase),
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
            Logger.LogWarning(ex, "dm-Direktsuche via Playwright fehlgeschlagen für Query '{Query}', Fallback auf Websuche.", request.Query);
        }

        return await base.SearchAsync(request, cancellationToken);
    }
}
