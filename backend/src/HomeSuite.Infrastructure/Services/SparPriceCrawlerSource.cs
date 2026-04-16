using System.Globalization;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using HomeSuite.Application.Interfaces;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace HomeSuite.Infrastructure.Services;

public class SparPriceCrawlerSource : IPriceCrawlerSource
{
    private static readonly CultureInfo DeAt = CultureInfo.GetCultureInfo("de-AT");

    private static readonly Regex PriceRegex = new(
        @"(?<!\d)(\d{1,3},\d{2})(?!\d)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex UnitPriceRegex = new(
        @"Per\s+1\s*(l|liter|kg|100\s*g|100g|stk|stück)\s+(\d{1,3},\d{2})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly HttpClient _httpClient;
    private readonly ILogger<SparPriceCrawlerSource> _logger;

    public SparPriceCrawlerSource(HttpClient httpClient, ILogger<SparPriceCrawlerSource> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("HomeSuiteCrawler", "1.0"));
        }
    }

    public async Task<List<CrawledCatalogPriceResult>> SearchAsync(
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

            var results = new List<CrawledCatalogPriceResult>();

            foreach (var productUrl in productUrls)
            {
                var result = await CrawlDirectProductPageAsync(productUrl, request, cancellationToken);
                if (result is not null)
                {
                    results.Add(result);
                }
            }

            return results
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SPAR-Crawler fehlgeschlagen für Query '{Query}'", request.Query);
            return [];
        }
    }

    private async Task<List<string>> SearchProductUrlsAsync(
        CrawledCatalogPriceRequest request,
        CancellationToken cancellationToken)
    {
        var query = $"{request.Query} {request.BrandHint}".Trim();
        var ddgQuery = Uri.EscapeDataString($"site:spar.at/produktwelt {query}");

        var searchUrl = $"https://html.duckduckgo.com/html/?q={ddgQuery}&kl=at-de";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, searchUrl);
        httpRequest.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        httpRequest.Headers.AcceptLanguage.ParseAdd("de-AT,de;q=0.9,en;q=0.8");

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("DuckDuckGo-Request für SPAR fehlgeschlagen: {StatusCode}", response.StatusCode);
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

        var urls = anchors
            .Select(a => a.GetAttributeValue("href", string.Empty))
            .Where(href => !string.IsNullOrWhiteSpace(href))
            .Select(TryExtractActualUrl)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Where(url => url!.Contains("spar.at/produktwelt/", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .Cast<string>()
            .ToList();

        return urls;
    }

    private async Task<CrawledCatalogPriceResult?> CrawlDirectProductPageAsync(
        string url,
        CrawledCatalogPriceRequest request,
        CancellationToken cancellationToken)
    {
        var html = await GetStringSafeAsync(url, cancellationToken);
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
            StoreName = "SPAR",
            ProductName = productName,
            UnitPrice = unitPrice,
            TotalPrice = totalPrice,
            ProductUrl = url,
            IsAvailable = true,
            SourceType = "crawler:spar"
        };
    }

    private async Task<string?> GetStringSafeAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("SPAR-Produktseite fehlgeschlagen: {StatusCode} {Url}", response.StatusCode, url);
            return null;
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static string? TryExtractActualUrl(string href)
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
            var uri = new Uri(href, UriKind.RelativeOrAbsolute);
            var query = uri.Query;
            if (string.IsNullOrWhiteSpace(query))
            {
                var idx = href.IndexOf('?');
                query = idx >= 0 ? href[(idx + 1)..] : string.Empty;
            }
            
            var parts = query.TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries);

            var uddg = parts
                .Select(p => p.Split('=', 2))
                .FirstOrDefault(p => p.Length == 2 && p[0] == "uddg");

            if (uddg is null)
            {
                return null;
            }

            return Uri.UnescapeDataString(uddg[1]);
        }
        catch
        {
            return null;
        }
    }

    private static decimal? ExtractDisplayPrice(string text)
    {
        var values = PriceRegex.Matches(text)
            .Select(m => ParseDecimal(m.Groups[1].Value))
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .Where(v => v is >= 0.20m and <= 200m)
            .ToList();

        if (values.Count == 0)
        {
            return null;
        }

        return values.Min();
    }

    private static decimal? ExtractUnitPrice(string text)
    {
        var match = UnitPriceRegex.Match(text);
        if (!match.Success)
        {
            return null;
        }

        return ParseDecimal(match.Groups[2].Value);
    }

    private static decimal? ParseDecimal(string value)
    {
        if (decimal.TryParse(value, NumberStyles.Number, DeAt, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static string NormalizeWhitespace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(HtmlEntity.DeEntitize(value), @"\s+", " ").Trim();
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
    }
}
