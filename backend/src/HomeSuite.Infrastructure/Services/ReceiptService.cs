using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using HomeSuite.Application.DTOs.Receipts;
using HomeSuite.Application.Interfaces;
using HomeSuite.Domain.Entities;
using HomeSuite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HomeSuite.Infrastructure.Services;

public sealed class ReceiptScanOptions
{
    public string Model { get; set; } = "claude-opus-4-8";
}

public class ReceiptService : IReceiptService
{
    private const string ReceiptSourceType = "receipt";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HomeSuiteDbContext _dbContext;
    private readonly IShoppingListService _shoppingListService;
    private readonly ICatalogPriceCrawlerService _catalogPriceCrawlerService;
    private readonly ReceiptScanOptions _options;
    private readonly ILogger<ReceiptService> _logger;

    public ReceiptService(
        HomeSuiteDbContext dbContext,
        IShoppingListService shoppingListService,
        ICatalogPriceCrawlerService catalogPriceCrawlerService,
        ReceiptScanOptions options,
        ILogger<ReceiptService> logger)
    {
        _dbContext = dbContext;
        _shoppingListService = shoppingListService;
        _catalogPriceCrawlerService = catalogPriceCrawlerService;
        _options = options;
        _logger = logger;
    }

    public async Task<ReceiptScanResultDto> ScanAsync(ScanReceiptRequest request, CancellationToken cancellationToken = default)
    {
        var imageData = NormalizeBase64(request.ImageBase64);
        if (string.IsNullOrWhiteSpace(imageData))
        {
            throw new ArgumentException("Es wurde kein Bild übermittelt.");
        }

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")))
        {
            throw new InvalidOperationException(
                "ANTHROPIC_API_KEY ist nicht gesetzt. Bitte den API-Key über das EnvironmentFile des Backends konfigurieren.");
        }

        var mediaType = MapMediaType(request.MediaType);

        var shoppingItems = request.ShoppingListId is { } listId
            ? await _dbContext.ShoppingItems
                .AsNoTracking()
                .Where(x => x.ShoppingListId == listId)
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken)
            : [];

        var catalogItems = await _dbContext.CatalogItems
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name })
            .ToListAsync(cancellationToken);

        var prompt = BuildPrompt(
            shoppingItems.Select(x => $"{x.Id} | {x.Name} | {x.Quantity:0.##} {x.Unit}"),
            catalogItems.Select(x => $"{x.Id} | {x.Name}"));

        AnthropicClient client = new();

        var parameters = new MessageCreateParams
        {
            Model = _options.Model,
            MaxTokens = 16000,
            Thinking = new ThinkingConfigAdaptive(),
            OutputConfig = new OutputConfig
            {
                Format = new JsonOutputFormat { Schema = BuildReceiptSchema() }
            },
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = new List<ContentBlockParam>
                    {
                        new ImageBlockParam(new Base64ImageSource
                        {
                            Data = imageData,
                            MediaType = mediaType
                        }),
                        new TextBlockParam { Text = prompt }
                    }
                }
            ]
        };

        var response = await client.Messages.Create(parameters);

        var json = response.Content
            .Select(block => block.Value)
            .OfType<TextBlock>()
            .FirstOrDefault()?.Text;

        if (string.IsNullOrWhiteSpace(json))
        {
            _logger.LogWarning("Beleg-Scan lieferte keine Textantwort. StopReason: {StopReason}", response.StopReason);
            throw new InvalidOperationException("Der Beleg konnte nicht ausgewertet werden (leere Antwort).");
        }

        var parsed = JsonSerializer.Deserialize<ParsedReceipt>(json, JsonOptions)
            ?? throw new InvalidOperationException("Der Beleg konnte nicht ausgewertet werden (ungültiges JSON).");

        return MapResult(parsed, shoppingItems.Select(x => x.Id).ToHashSet(), catalogItems.Select(x => x.Id).ToHashSet());
    }

    public async Task ApplyAsync(ApplyReceiptRequest request, CancellationToken cancellationToken = default)
    {
        var shoppingList = await _dbContext.ShoppingLists
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == request.ShoppingListId, cancellationToken);

        if (shoppingList is null)
        {
            throw new InvalidOperationException("Einkaufsliste nicht gefunden.");
        }

        var storeName = string.IsNullOrWhiteSpace(request.StoreName) ? "Beleg" : request.StoreName.Trim();
        var affectedCatalogItemIds = new HashSet<Guid>();

        foreach (var line in request.Lines)
        {
            if (line.TotalPrice < 0 || string.IsNullOrWhiteSpace(line.Name))
            {
                continue;
            }

            ShoppingItem? shoppingItem = null;

            if (line.ShoppingItemId is { } shoppingItemId)
            {
                shoppingItem = shoppingList.Items.FirstOrDefault(x => x.Id == shoppingItemId);
                if (shoppingItem is not null)
                {
                    shoppingItem.ActualTotalPrice = line.TotalPrice;
                    shoppingItem.PurchasedQuantity = line.Quantity > 0 ? line.Quantity : shoppingItem.Quantity;
                    shoppingItem.IsChecked = true;
                }
            }

            // Bei korrigierter Zuordnung gilt der Katalogartikel des Einkaufslisten-Artikels,
            // nicht der ursprünglich vom Scan vorgeschlagene.
            var catalogItemId = shoppingItem?.CatalogItemId ?? line.CatalogItemId;
            if (catalogItemId is null || line.TotalPrice <= 0)
            {
                continue;
            }

            var catalogItemExists = await _dbContext.CatalogItems
                .AnyAsync(x => x.Id == catalogItemId.Value, cancellationToken);

            if (!catalogItemExists)
            {
                continue;
            }

            var existingPrices = await _dbContext.CatalogItemPrices
                .Where(x => x.CatalogItemId == catalogItemId.Value)
                .Where(x => x.SourceType == ReceiptSourceType)
                .Where(x => x.StoreName == storeName)
                .ToListAsync(cancellationToken);

            if (existingPrices.Count > 0)
            {
                _dbContext.CatalogItemPrices.RemoveRange(existingPrices);
            }

            _dbContext.CatalogItemPrices.Add(new CatalogItemPrice
            {
                Id = Guid.NewGuid(),
                CatalogItemId = catalogItemId.Value,
                StoreName = storeName,
                ProductName = line.Name.Trim(),
                UnitPrice = line.UnitPrice,
                TotalPrice = line.Quantity > 1 ? decimal.Round(line.TotalPrice / line.Quantity, 2) : line.TotalPrice,
                ProductUrl = null,
                IsAvailable = true,
                CheckedAt = DateTime.UtcNow,
                SourceType = ReceiptSourceType
            });

            affectedCatalogItemIds.Add(catalogItemId.Value);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var catalogItemId in affectedCatalogItemIds)
        {
            await _catalogPriceCrawlerService.RecordPriceSnapshotAsync(catalogItemId, cancellationToken);
        }

        if (request.CompleteList)
        {
            await _shoppingListService.CompleteShoppingListAsync(request.ShoppingListId, request.Complete, cancellationToken);
        }
    }

    private static string NormalizeBase64(string value)
    {
        var trimmed = value.Trim();

        // data:image/jpeg;base64,... → reinen Base64-Teil verwenden
        if (trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var commaIndex = trimmed.IndexOf(',');
            if (commaIndex >= 0)
            {
                trimmed = trimmed[(commaIndex + 1)..];
            }
        }

        return trimmed;
    }

    private static MediaType MapMediaType(string mediaType)
    {
        return mediaType.Trim().ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => MediaType.ImageJpeg,
            "image/png" => MediaType.ImagePng,
            "image/webp" => MediaType.ImageWebP,
            "image/gif" => MediaType.ImageGif,
            _ => throw new ArgumentException($"Nicht unterstütztes Bildformat: {mediaType}")
        };
    }

    private static string BuildPrompt(IEnumerable<string> shoppingItemLines, IEnumerable<string> catalogItemLines)
    {
        var builder = new StringBuilder();

        builder.AppendLine("Du analysierst das Foto eines Kassenbons (Österreich/Deutschland).");
        builder.AppendLine("Extrahiere alle gekauften Artikel:");
        builder.AppendLine("- Eine Zeile pro gekauftem Artikel mit dem tatsächlich bezahlten Endpreis (Rabatte/Aktionen dem Artikel zurechnen).");
        builder.AppendLine("- Bezahltes Pfand als eigene Zeile aufnehmen, zurückgegebenes Pfand und reine Rabatt-Sammelzeilen weglassen.");
        builder.AppendLine("- Keine Zwischensummen-, Zahlungs-, Rückgeld- oder Bonuspunkte-Zeilen.");
        builder.AppendLine("- quantity ist die Stückzahl bzw. das Gewicht in kg; bei unbekannter Menge 1.");
        builder.AppendLine("- unitPrice nur setzen, wenn er auf dem Bon steht oder sich eindeutig ergibt, sonst null.");
        builder.AppendLine("- storeName ist die Handelskette (z. B. BILLA, SPAR, Hofer, Lidl, dm, BIPA), purchaseDate das Bon-Datum als yyyy-MM-dd, totalAmount die Bon-Gesamtsumme.");
        builder.AppendLine();
        builder.AppendLine("Ordne jede Artikelzeile zu, wenn es eindeutig möglich ist:");
        builder.AppendLine("- shoppingItemId: die ID des passenden Einkaufslisten-Artikels aus der Liste unten.");
        builder.AppendLine("- catalogItemId: die ID des passenden Katalogartikels aus der Liste unten.");
        builder.AppendLine("Verwende ausschließlich IDs aus den Listen. Im Zweifel null setzen statt zu raten.");
        builder.AppendLine();

        builder.AppendLine("Einkaufslisten-Artikel (id | name | menge):");
        var hasShoppingItems = false;
        foreach (var line in shoppingItemLines)
        {
            builder.AppendLine(line);
            hasShoppingItems = true;
        }

        if (!hasShoppingItems)
        {
            builder.AppendLine("(keine)");
        }

        builder.AppendLine();
        builder.AppendLine("Katalogartikel (id | name):");
        var hasCatalogItems = false;
        foreach (var line in catalogItemLines)
        {
            builder.AppendLine(line);
            hasCatalogItems = true;
        }

        if (!hasCatalogItems)
        {
            builder.AppendLine("(keine)");
        }

        return builder.ToString();
    }

    private static Dictionary<string, JsonElement> BuildReceiptSchema()
    {
        var schema = new
        {
            type = "object",
            properties = new
            {
                storeName = new { type = new[] { "string", "null" } },
                purchaseDate = new { type = new[] { "string", "null" } },
                totalAmount = new { type = new[] { "number", "null" } },
                lines = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            name = new { type = "string" },
                            quantity = new { type = "number" },
                            unitPrice = new { type = new[] { "number", "null" } },
                            totalPrice = new { type = "number" },
                            shoppingItemId = new { type = new[] { "string", "null" } },
                            catalogItemId = new { type = new[] { "string", "null" } }
                        },
                        required = new[] { "name", "quantity", "unitPrice", "totalPrice", "shoppingItemId", "catalogItemId" },
                        additionalProperties = false
                    }
                }
            },
            required = new[] { "storeName", "purchaseDate", "totalAmount", "lines" },
            additionalProperties = false
        };

        return JsonSerializer.SerializeToElement(schema).Deserialize<Dictionary<string, JsonElement>>()!;
    }

    private static ReceiptScanResultDto MapResult(
        ParsedReceipt parsed,
        IReadOnlySet<Guid> knownShoppingItemIds,
        IReadOnlySet<Guid> knownCatalogItemIds)
    {
        var result = new ReceiptScanResultDto
        {
            StoreName = string.IsNullOrWhiteSpace(parsed.StoreName) ? null : parsed.StoreName.Trim(),
            PurchaseDate = string.IsNullOrWhiteSpace(parsed.PurchaseDate) ? null : parsed.PurchaseDate.Trim(),
            TotalAmount = parsed.TotalAmount
        };

        foreach (var line in parsed.Lines ?? [])
        {
            if (string.IsNullOrWhiteSpace(line.Name))
            {
                continue;
            }

            // IDs nur übernehmen, wenn sie wirklich existieren — das Modell darf nicht raten.
            Guid? shoppingItemId = Guid.TryParse(line.ShoppingItemId, out var sid) && knownShoppingItemIds.Contains(sid)
                ? sid
                : null;
            Guid? catalogItemId = Guid.TryParse(line.CatalogItemId, out var cid) && knownCatalogItemIds.Contains(cid)
                ? cid
                : null;

            result.Lines.Add(new ReceiptLineDto
            {
                Name = line.Name.Trim(),
                Quantity = line.Quantity > 0 ? line.Quantity : 1,
                UnitPrice = line.UnitPrice,
                TotalPrice = line.TotalPrice,
                ShoppingItemId = shoppingItemId,
                CatalogItemId = catalogItemId
            });
        }

        return result;
    }

    private sealed record ParsedReceipt(
        string? StoreName,
        string? PurchaseDate,
        decimal? TotalAmount,
        List<ParsedLine>? Lines);

    private sealed record ParsedLine(
        string? Name,
        decimal Quantity,
        decimal? UnitPrice,
        decimal TotalPrice,
        string? ShoppingItemId,
        string? CatalogItemId);
}
