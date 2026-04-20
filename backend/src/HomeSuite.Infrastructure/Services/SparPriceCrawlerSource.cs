using Microsoft.Extensions.Logging;
using HomeSuite.Application.Interfaces;

namespace HomeSuite.Infrastructure.Services;

public class SparPriceCrawlerSource : HtmlSearchPriceCrawlerSourceBase
{
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
            Logger.LogWarning(ex, "SPAR-Direktsuche fehlgeschlagen für Query '{Query}', Fallback auf Websuche.", request.Query);
        }

        return await base.SearchAsync(request, cancellationToken);
    }

    private async Task<List<string>> SearchDirectProductUrlsAsync(
        CrawledCatalogPriceRequest request,
        CancellationToken cancellationToken)
    {
        var url = $"https://www.spar.at/suche?q={Uri.EscapeDataString(request.Query.Trim())}";

        var playwrightUrls = await SearchProductUrlsWithPlaywrightAsync(
            url,
            ["/produkt", "/produktwelt/"],
            urlValue => urlValue.Contains("/produkt", StringComparison.OrdinalIgnoreCase)
                || urlValue.Contains("/produktwelt/", StringComparison.OrdinalIgnoreCase),
            cancellationToken);

        if (playwrightUrls.Count > 0)
        {
            return playwrightUrls;
        }

        return [];
    }
}
