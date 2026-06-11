using System.Globalization;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using HomeSuite.Application.Interfaces;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace HomeSuite.Infrastructure.Services;

public class BillaPriceCrawlerSource : IPriceCrawlerSource
{
    private static readonly CultureInfo DeAt = CultureInfo.GetCultureInfo("de-AT");

    private static readonly Regex PriceRegex = new(
        @"(?<!\d)(\d{1,3},\d{2})\s*€",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PackageRegex = new(
        @"(\d+(?:,\d+)?)\s*(liter|l|ml|kg|g|stk|stück)\s+packung",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex UnitPriceRegex = new(
        @"1\s*(liter|l|kg|100\s*g|100g|stk|stück)\s+(\d{1,3},\d{2})\s*€",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Steht direkt vor einem Preis "je 1 Stk" o. Ä., ist es ein Grundpreis und
    // darf nicht als Produktpreis gewertet werden.
    private static readonly Regex UnitPriceContextRegex = new(
        @"(?:je|per)?\s*1\s*(?:liter|l|kg|100\s*g|100g|stk|stück|st|wl)\.?\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly HttpClient _httpClient;
    private readonly ILogger<BillaPriceCrawlerSource> _logger;

    public BillaPriceCrawlerSource(HttpClient httpClient, ILogger<BillaPriceCrawlerSource> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = new Uri("https://shop.billa.at/");
        }

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
            if (TryGetBillaUrl(request.Query, out var directUrl))
            {
                var directResult = await CrawlDirectPageAsync(directUrl, request, cancellationToken);
                return directResult is null ? [] : [directResult];
            }

            return await SearchResultsAsync(request, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BILLA-Crawler fehlgeschlagen für Query '{Query}'", request.Query);
            return [];
        }
    }

    private async Task<List<CrawledCatalogPriceResult>> SearchResultsAsync(
        CrawledCatalogPriceRequest request,
        CancellationToken cancellationToken)
    {
        var query = Uri.EscapeDataString(request.Query.Trim());
        var url = $"https://shop.billa.at/suche/{query}";

        var html = await GetStringSafeAsync(url, cancellationToken);
        if (string.IsNullOrWhiteSpace(html))
        {
            return [];
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var links = doc.DocumentNode.SelectNodes("//a[@href]");
        if (links is null || links.Count == 0)
        {
            return [];
        }

        var queryTokens = BuildTokens(request.Query, null);
        var brandTokens = BuildTokens(request.BrandHint ?? string.Empty, null);
        var results = new List<(int Score, CrawledCatalogPriceResult Result)>();

        foreach (var link in links)
        {
            var href = link.GetAttributeValue("href", string.Empty);
            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            if (!href.Contains("/produkte/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var cardNode = FindBestCardNode(link);
            if (cardNode is null)
            {
                continue;
            }

            var cardText = NormalizeWhitespace(cardNode.InnerText);
            if (string.IsNullOrWhiteSpace(cardText) || !cardText.Contains('€'))
            {
                continue;
            }

            var productName = ExtractProductName(cardNode, link);
            if (string.IsNullOrWhiteSpace(productName))
            {
                continue;
            }

            var totalPrice = ExtractDisplayPrice(cardText);
            if (totalPrice is null)
            {
                continue;
            }

            var score = ScoreProduct(productName, cardText, queryTokens, brandTokens);
            if (score <= 0)
            {
                continue;
            }

            var productUrl = MakeAbsoluteUrl(href);
            var unitPrice = ExtractUnitPrice(cardText);

            results.Add((score, new CrawledCatalogPriceResult
            {
                StoreName = "BILLA",
                ProductName = productName,
                UnitPrice = unitPrice,
                TotalPrice = totalPrice,
                ProductUrl = productUrl,
                IsAvailable = true,
                SourceType = "crawler:billa"
            }));
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

    private async Task<CrawledCatalogPriceResult?> CrawlDirectPageAsync(
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
        var jsonLdProduct = JsonLdProductParser.ExtractProduct(doc);

        var productName = FirstNonEmpty(
            jsonLdProduct?.Name,
            doc.DocumentNode.SelectSingleNode("//h1")?.InnerText,
            doc.DocumentNode.SelectSingleNode("//title")?.InnerText,
            request.Query);

        var totalPrice = jsonLdProduct?.Price ?? ExtractDisplayPrice(pageText);
        var unitPrice = ExtractUnitPrice(pageText);

        if (totalPrice is null)
        {
            return null;
        }

        return new CrawledCatalogPriceResult
        {
            StoreName = "BILLA",
            ProductName = productName,
            UnitPrice = unitPrice,
            TotalPrice = totalPrice,
            ProductUrl = url,
            IsAvailable = true,
            SourceType = "crawler:billa"
        };
    }

    private async Task<string?> GetStringSafeAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("BILLA-Request fehlgeschlagen: {StatusCode} {Url}", response.StatusCode, url);
            return null;
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static HtmlNode? FindBestCardNode(HtmlNode startNode)
    {
        HtmlNode? current = startNode;

        for (var i = 0; i < 8 && current is not null; i++)
        {
            var text = NormalizeWhitespace(current.InnerText);
            if (text.Contains('€') && text.Length is > 20 and < 2000)
            {
                return current;
            }

            current = current.ParentNode;
        }

        return null;
    }

    private static string ExtractProductName(HtmlNode cardNode, HtmlNode linkNode)
    {
        var headingNode = cardNode.SelectSingleNode(".//h1|.//h2|.//h3|.//h4|.//h5|.//h6");

        var headingText = NormalizeWhitespace(headingNode?.InnerText);
        if (!string.IsNullOrWhiteSpace(headingText))
        {
            return headingText;
        }

        var linkText = NormalizeWhitespace(linkNode.InnerText);
        if (!string.IsNullOrWhiteSpace(linkText))
        {
            return linkText;
        }

        var lines = cardNode.InnerText
            .Split('\n')
            .Select(NormalizeWhitespace)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        return lines.FirstOrDefault(x => !x.Contains('€')) ?? string.Empty;
    }

    private static decimal? ExtractDisplayPrice(string text)
    {
        var matches = PriceRegex.Matches(text);
        if (matches.Count == 0)
        {
            return null;
        }

        var values = matches
            .Where(m => !IsUnitPriceContext(text, m.Index))
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

    private static bool IsUnitPriceContext(string text, int priceIndex)
    {
        var contextStart = Math.Max(0, priceIndex - 20);
        return UnitPriceContextRegex.IsMatch(text[contextStart..priceIndex]);
    }

    private static decimal? ExtractUnitPrice(string text)
    {
        var match = UnitPriceRegex.Match(text);
        return match.Success ? ParseDecimal(match.Groups[2].Value) : null;
    }

    private static decimal? ParseDecimal(string value)
    {
        if (decimal.TryParse(value, NumberStyles.Number, DeAt, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static int ScoreProduct(string productName, string cardText, IReadOnlyList<string> tokens, IReadOnlyList<string> brandTokens)
    {
        var haystackName = productName.ToLowerInvariant();
        var haystackCard = cardText.ToLowerInvariant();

        var score = 0;
        var nameHits = 0;

        foreach (var token in tokens)
        {
            if (haystackName.Contains(token))
            {
                score += 3;
                nameHits++;
            }
            else if (haystackCard.Contains(token))
            {
                score += 1;
            }
        }

        // Ohne Treffer im Produktnamen ist das Produkt mit hoher Wahrscheinlichkeit irrelevant.
        if (nameHits == 0)
        {
            return 0;
        }

        foreach (var token in brandTokens)
        {
            if (haystackName.Contains(token))
            {
                score += 5;
            }
        }

        if (LooksLikeRelevantPackage(cardText))
        {
            score += 1;
        }

        return score;
    }

    private static bool LooksLikeRelevantPackage(string text)
    {
        return PackageRegex.IsMatch(text);
    }

    private static IReadOnlyList<string> BuildTokens(string query, string? brandHint)
    {
        var raw = $"{query} {brandHint}".ToLowerInvariant();

        return raw
            .Split([' ', '-', ',', '.', ';', ':', '/', '\\', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length >= 2)
            .Distinct()
            .ToList();
    }

    private static bool TryGetBillaUrl(string value, out string url)
    {
        url = string.Empty;

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!uri.Host.Contains("billa.at", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        url = uri.ToString();
        return true;
    }

    private static string MakeAbsoluteUrl(string href)
    {
        if (Uri.TryCreate(href, UriKind.Absolute, out var absolute))
        {
            return absolute.ToString();
        }

        return $"https://shop.billa.at{href}";
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
