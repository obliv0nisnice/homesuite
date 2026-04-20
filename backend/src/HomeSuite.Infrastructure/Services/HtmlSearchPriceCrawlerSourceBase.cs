using System.Globalization;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using HomeSuite.Application.Interfaces;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace HomeSuite.Infrastructure.Services;

public abstract class HtmlSearchPriceCrawlerSourceBase : IPriceCrawlerSource
{
    private static readonly CultureInfo DeAt = CultureInfo.GetCultureInfo("de-AT");

    private static readonly Regex PriceRegex = new(
        @"(?<!\d)(\d{1,3},\d{2})\s*€?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex UnitPriceRegex = new(
        @"(?:je|per)\s*1\s*(l|liter|kg|100\s*g|100g|stk|stück)\s*(\d{1,3},\d{2})\s*€?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    protected HttpClient HttpClient { get; }
    protected ILogger Logger { get; }
    protected IPlaywrightStoreSearchService? PlaywrightSearchService { get; }

    protected HtmlSearchPriceCrawlerSourceBase(
        HttpClient httpClient,
        ILogger logger,
        IPlaywrightStoreSearchService? playwrightSearchService = null)
    {
        HttpClient = httpClient;
        Logger = logger;
        PlaywrightSearchService = playwrightSearchService;

        if (HttpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            HttpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("HomeSuiteCrawler", "1.0"));
        }
    }

    protected abstract string StoreName { get; }
    protected abstract string StoreDomain { get; }
    protected abstract string SourceType { get; }

    public virtual async Task<List<CrawledCatalogPriceResult>> SearchAsync(
        CrawledCatalogPriceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return [];
        }

        try
        {
            var productUrls = await SearchProductUrlsAsync(request, cancellationToken);
            if (productUrls.Count == 0)
            {
                return [];
            }

            return await BuildRankedResultsAsync(productUrls, request, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "{StoreName}-Crawler fehlgeschlagen für Query '{Query}'", StoreName, request.Query);
            return [];
        }
    }

    protected async Task<List<CrawledCatalogPriceResult>> BuildRankedResultsAsync(
        IReadOnlyList<string> productUrls,
        CrawledCatalogPriceRequest request,
        CancellationToken cancellationToken)
    {
        var queryTokens = BuildTokens(request.Query, request.BrandHint);
        var results = new List<(int Score, CrawledCatalogPriceResult Result)>();

        foreach (var productUrl in productUrls)
        {
            var result = await CrawlDirectProductPageAsync(productUrl, request, cancellationToken);
            if (result is null)
            {
                continue;
            }

            var score = ScoreProduct(result.ProductName, request.Query, queryTokens);
            if (score > 0)
            {
                results.Add((score, result));
            }
        }

        return results
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Result.TotalPrice ?? decimal.MaxValue)
            .Select(x => x.Result)
            .GroupBy(x => new
            {
                Url = (x.ProductUrl ?? string.Empty).Trim().ToLowerInvariant(),
                Name = x.ProductName.Trim().ToLowerInvariant(),
                Total = x.TotalPrice,
                Unit = x.UnitPrice
            })
            .Select(x => x.First())
            .Take(3)
            .ToList();
    }

    protected virtual async Task<List<string>> SearchProductUrlsAsync(
        CrawledCatalogPriceRequest request,
        CancellationToken cancellationToken)
    {
        var query = $"{request.Query} {request.BrandHint}".Trim();
        var ddgQuery = Uri.EscapeDataString($"site:{StoreDomain} {query}");
        var searchUrl = $"https://html.duckduckgo.com/html/?q={ddgQuery}&kl=at-de";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, searchUrl);
        httpRequest.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        httpRequest.Headers.AcceptLanguage.ParseAdd("de-AT,de;q=0.9,en;q=0.8");

        using var response = await HttpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            Logger.LogWarning("{StoreName}-Suchrequest fehlgeschlagen: {StatusCode}", StoreName, response.StatusCode);
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
            .Where(href => !string.IsNullOrWhiteSpace(href))
            .Select(TryExtractActualUrl)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Where(IsStoreUrl)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .Cast<string>()
            .ToList();
    }

    protected async Task<List<string>> SearchProductUrlsWithPlaywrightAsync(
        string searchUrl,
        IReadOnlyList<string> preferredUrlFragments,
        Func<string, bool> urlFilter,
        CancellationToken cancellationToken)
    {
        if (PlaywrightSearchService is null || string.IsNullOrWhiteSpace(searchUrl))
        {
            return [];
        }

        var result = await PlaywrightSearchService.SearchAsync(new PlaywrightStoreSearchRequest
        {
            SearchUrl = searchUrl,
            Host = StoreDomain,
            ResultLimit = 50,
            PreferredUrlFragments = preferredUrlFragments.ToList()
        }, cancellationToken);

        if (result is null || result.Links.Count == 0)
        {
            return [];
        }

        return result.Links
            .Where(IsStoreUrl)
            .Where(urlFilter)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();
    }

    protected virtual async Task<CrawledCatalogPriceResult?> CrawlDirectProductPageAsync(
        string url,
        CrawledCatalogPriceRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await HttpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            Logger.LogWarning("{StoreName}-Produktseite fehlgeschlagen: {StatusCode} {Url}", StoreName, response.StatusCode, url);
            return null;
        }

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var pageText = NormalizeWhitespace(doc.DocumentNode.InnerText);
        if (string.IsNullOrWhiteSpace(pageText))
        {
            return null;
        }

        var productName = FirstNonEmpty(
            doc.DocumentNode.SelectSingleNode("//h1")?.InnerText,
            doc.DocumentNode.SelectSingleNode("//title")?.InnerText,
            request.Query);

        var totalPrice = ExtractDisplayPrice(pageText);
        var unitPrice = ExtractUnitPrice(pageText);

        if (totalPrice is null)
        {
            return null;
        }

        return new CrawledCatalogPriceResult
        {
            StoreName = StoreName,
            ProductName = productName,
            UnitPrice = unitPrice,
            TotalPrice = totalPrice,
            ProductUrl = url,
            IsAvailable = true,
            SourceType = SourceType
        };
    }

    protected bool IsStoreUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Host.Contains(StoreDomain, StringComparison.OrdinalIgnoreCase);
    }

    protected static string? TryExtractActualUrl(string href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        if (href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            href.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return href;
        }

        if (!href.Contains("uddg=", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            var idx = href.IndexOf("uddg=", StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                return null;
            }

            var encoded = href[(idx + 5)..];
            var ampersand = encoded.IndexOf('&');
            if (ampersand >= 0)
            {
                encoded = encoded[..ampersand];
            }

            return Uri.UnescapeDataString(encoded);
        }
        catch
        {
            return null;
        }
    }

    protected static decimal? ExtractDisplayPrice(string text)
    {
        var values = PriceRegex.Matches(text)
            .Select(m => ParseDecimal(m.Groups[1].Value))
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .Where(v => v is >= 0.20m and <= 200m)
            .ToList();

        return values.Count == 0 ? null : values.Min();
    }

    protected static decimal? ExtractUnitPrice(string text)
    {
        var match = UnitPriceRegex.Match(text);
        return match.Success ? ParseDecimal(match.Groups[2].Value) : null;
    }

    protected static decimal? ParseDecimal(string value)
    {
        return decimal.TryParse(value, NumberStyles.Number, DeAt, out var parsed) ? parsed : null;
    }

    protected static IReadOnlyList<string> BuildTokens(string query, string? brandHint)
    {
        var raw = $"{query} {brandHint}".ToLowerInvariant();

        return raw
            .Split([' ', '-', ',', '.', ';', ':', '/', '\\', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length >= 2)
            .Distinct()
            .ToList();
    }

    protected static int ScoreProduct(string productName, string pageText, IReadOnlyList<string> tokens)
    {
        var haystackName = productName.ToLowerInvariant();
        var haystackPage = pageText.ToLowerInvariant();
        var score = 0;

        foreach (var token in tokens)
        {
            if (haystackName.Contains(token))
            {
                score += 3;
            }
            else if (haystackPage.Contains(token))
            {
                score += 1;
            }
        }

        return score;
    }

    protected static string NormalizeWhitespace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(HtmlEntity.DeEntitize(value), @"\s+", " ").Trim();
    }

    protected static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
    }
}
