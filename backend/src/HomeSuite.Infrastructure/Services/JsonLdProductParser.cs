using System.Globalization;
using System.Text.Json;
using HtmlAgilityPack;

namespace HomeSuite.Infrastructure.Services;

public sealed record JsonLdProduct(string? Name, decimal? Price);

/// <summary>
/// Liest schema.org-Product-Daten (JSON-LD) aus einer Produktseite. Deutlich zuverlässiger als
/// Preis-Regex über den Seitentext, weil Cross-Selling-Preise nicht mitgelesen werden.
/// </summary>
public static class JsonLdProductParser
{
    public static JsonLdProduct? ExtractProduct(HtmlDocument doc)
    {
        var scriptNodes = doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
        if (scriptNodes is null)
        {
            return null;
        }

        foreach (var scriptNode in scriptNodes)
        {
            var json = scriptNode.InnerText;
            if (string.IsNullOrWhiteSpace(json))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(json);
                var product = FindProductNode(document.RootElement);
                if (product is not null)
                {
                    return product;
                }
            }
            catch (JsonException)
            {
                // Ungültiges JSON-LD ignorieren, Regex-Fallback greift.
            }
        }

        return null;
    }

    private static JsonLdProduct? FindProductNode(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var entry in element.EnumerateArray())
                {
                    var found = FindProductNode(entry);
                    if (found is not null)
                    {
                        return found;
                    }
                }

                return null;

            case JsonValueKind.Object:
                if (IsType(element, "Product"))
                {
                    var name = element.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String
                        ? nameProp.GetString()
                        : null;
                    var price = element.TryGetProperty("offers", out var offers)
                        ? ExtractOfferPrice(offers)
                        : null;

                    if (price is not null || !string.IsNullOrWhiteSpace(name))
                    {
                        return new JsonLdProduct(name, price);
                    }
                }

                if (element.TryGetProperty("@graph", out var graph))
                {
                    return FindProductNode(graph);
                }

                return null;

            default:
                return null;
        }
    }

    private static bool IsType(JsonElement element, string type)
    {
        if (!element.TryGetProperty("@type", out var typeProp))
        {
            return false;
        }

        return typeProp.ValueKind switch
        {
            JsonValueKind.String => string.Equals(typeProp.GetString(), type, StringComparison.OrdinalIgnoreCase),
            JsonValueKind.Array => typeProp.EnumerateArray().Any(x =>
                x.ValueKind == JsonValueKind.String &&
                string.Equals(x.GetString(), type, StringComparison.OrdinalIgnoreCase)),
            _ => false
        };
    }

    private static decimal? ExtractOfferPrice(JsonElement offers)
    {
        switch (offers.ValueKind)
        {
            case JsonValueKind.Array:
                return offers.EnumerateArray()
                    .Select(ExtractOfferPrice)
                    .Where(x => x.HasValue)
                    .Min();

            case JsonValueKind.Object:
                foreach (var propertyName in new[] { "price", "lowPrice" })
                {
                    if (!offers.TryGetProperty(propertyName, out var priceProp))
                    {
                        continue;
                    }

                    var price = priceProp.ValueKind switch
                    {
                        JsonValueKind.Number => priceProp.GetDecimal(),
                        JsonValueKind.String when decimal.TryParse(
                            priceProp.GetString(),
                            NumberStyles.Number,
                            CultureInfo.InvariantCulture,
                            out var parsed) => parsed,
                        _ => (decimal?)null
                    };

                    if (price is > 0)
                    {
                        return price;
                    }
                }

                return null;

            default:
                return null;
        }
    }
}
