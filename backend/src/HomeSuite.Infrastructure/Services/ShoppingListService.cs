using HomeSuite.Application.DTOs.ShoppingLists;
using HomeSuite.Application.Interfaces;
using HomeSuite.Domain.Entities;
using HomeSuite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace HomeSuite.Infrastructure.Services;

public class ShoppingListService : IShoppingListService
{
    private readonly HomeSuiteDbContext _dbContext;
    private readonly UpcomingMealPlanShoppingListSyncService _shoppingListSyncService;

    public ShoppingListService(
        HomeSuiteDbContext dbContext,
        UpcomingMealPlanShoppingListSyncService shoppingListSyncService)
    {
        _dbContext = dbContext;
        _shoppingListSyncService = shoppingListSyncService;
    }

    public async Task<List<ShoppingListDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.ShoppingLists
            .Include(x => x.Items)
                .ThenInclude(x => x.PriceOptions)
            .Include(x => x.Items)
                .ThenInclude(x => x.CatalogItem)
            .OrderByDescending(x => x.CreatedAt)
            .Select(MapToDto())
            .ToListAsync(cancellationToken);
    }

    public async Task<ShoppingListDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ShoppingLists
            .Include(x => x.Items)
                .ThenInclude(x => x.PriceOptions)
            .Include(x => x.Items)
                .ThenInclude(x => x.CatalogItem)
            .Where(x => x.Id == id)
            .Select(MapToDto())
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ShoppingListDto> CreateAsync(CreateShoppingListRequest request, CancellationToken cancellationToken = default)
    {
        ValidateShoppingList(request.Name);

        var shoppingList = new ShoppingList
        {
            Name = request.Name.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ShoppingLists.Add(shoppingList);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(shoppingList.Id, cancellationToken)
            ?? throw new InvalidOperationException("Einkaufsliste konnte nicht geladen werden.");
    }

    public async Task<ShoppingListDto?> UpdateAsync(Guid id, UpdateShoppingListRequest request, CancellationToken cancellationToken = default)
    {
        ValidateShoppingList(request.Name);

        var shoppingList = await _dbContext.ShoppingLists
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (shoppingList is null)
        {
            return null;
        }

        shoppingList.Name = request.Name.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(shoppingList.Id, cancellationToken);
    }

    public async Task<ShoppingItemDto> AddItemAsync(Guid shoppingListId, CreateShoppingItemRequest request, CancellationToken cancellationToken = default)
    {
        var shoppingListExists = await _dbContext.ShoppingLists
            .AnyAsync(x => x.Id == shoppingListId, cancellationToken);

        if (!shoppingListExists)
        {
            throw new InvalidOperationException("Einkaufsliste nicht gefunden.");
        }

        var item = CreateItemEntity(request);
        item.ShoppingListId = shoppingListId;

        _dbContext.ShoppingItems.Add(item);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetShoppingItemAsync(item.Id, cancellationToken)
            ?? throw new InvalidOperationException("Einkaufsposten konnte nicht geladen werden.");
    }

    public async Task<ShoppingItemDto?> UpdateItemAsync(Guid shoppingListId, Guid itemId, UpdateShoppingItemRequest request, CancellationToken cancellationToken = default)
    {
        ValidateShoppingItem(request.Name, request.Quantity);

        var item = await _dbContext.ShoppingItems
            .FirstOrDefaultAsync(x => x.Id == itemId && x.ShoppingListId == shoppingListId, cancellationToken);

        if (item is null)
        {
            return null;
        }

        item.Name = request.Name.Trim();
        item.RequiredQuantity = request.RequiredQuantity;
        item.InventoryQuantityUsed = request.InventoryQuantityUsed;
        item.Quantity = request.Quantity;
        item.PurchasedQuantity = request.PurchasedQuantity;
        item.Unit = string.IsNullOrWhiteSpace(request.Unit) ? "Stk" : request.Unit.Trim();
        item.IsChecked = request.IsChecked;
        item.EstimatedUnitPrice = request.EstimatedUnitPrice;
        item.EstimatedTotalPrice = request.EstimatedTotalPrice;
        item.ActualTotalPrice = request.ActualTotalPrice;
        item.SourceType = string.IsNullOrWhiteSpace(request.SourceType) ? "Manual" : request.SourceType.Trim();
        item.CatalogItemId = request.CatalogItemId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetShoppingItemAsync(item.Id, cancellationToken);
    }

    public async Task<bool> DeleteItemAsync(Guid shoppingListId, Guid itemId, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.ShoppingItems
            .FirstOrDefaultAsync(x => x.Id == itemId && x.ShoppingListId == shoppingListId, cancellationToken);

        if (item is null)
        {
            return false;
        }

        _dbContext.ShoppingItems.Remove(item);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ShoppingItemPriceOptionDto> AddPriceOptionAsync(
        Guid shoppingListId,
        Guid itemId,
        CreateShoppingItemPriceOptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.ShoppingItems
            .FirstOrDefaultAsync(x => x.Id == itemId && x.ShoppingListId == shoppingListId, cancellationToken);

        if (item is null)
        {
            throw new InvalidOperationException("Einkaufsposten nicht gefunden.");
        }

        ValidatePriceOption(request.StoreName, request.ProductName, request.UnitPrice, request.TotalPrice);

        var priceOption = new ShoppingItemPriceOption
        {
            ShoppingItemId = itemId,
            StoreName = request.StoreName.Trim(),
            ProductName = request.ProductName.Trim(),
            UnitPrice = request.UnitPrice,
            TotalPrice = request.TotalPrice,
            ProductUrl = string.IsNullOrWhiteSpace(request.ProductUrl) ? null : request.ProductUrl.Trim(),
            IsAvailable = request.IsAvailable,
            CheckedAt = request.CheckedAt ?? DateTime.UtcNow
        };

        _dbContext.ShoppingItemPriceOptions.Add(priceOption);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapPriceOption(priceOption);
    }

    public async Task<ShoppingItemPriceOptionDto?> UpdatePriceOptionAsync(
        Guid shoppingListId,
        Guid itemId,
        Guid priceOptionId,
        UpdateShoppingItemPriceOptionRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidatePriceOption(request.StoreName, request.ProductName, request.UnitPrice, request.TotalPrice);

        var priceOption = await _dbContext.ShoppingItemPriceOptions
            .Include(x => x.ShoppingItem)
            .FirstOrDefaultAsync(
                x => x.Id == priceOptionId &&
                     x.ShoppingItemId == itemId &&
                     x.ShoppingItem != null &&
                     x.ShoppingItem.ShoppingListId == shoppingListId,
                cancellationToken);

        if (priceOption is null)
        {
            return null;
        }

        priceOption.StoreName = request.StoreName.Trim();
        priceOption.ProductName = request.ProductName.Trim();
        priceOption.UnitPrice = request.UnitPrice;
        priceOption.TotalPrice = request.TotalPrice;
        priceOption.ProductUrl = string.IsNullOrWhiteSpace(request.ProductUrl) ? null : request.ProductUrl.Trim();
        priceOption.IsAvailable = request.IsAvailable;
        priceOption.CheckedAt = request.CheckedAt ?? priceOption.CheckedAt;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapPriceOption(priceOption);
    }

    public async Task<bool> DeletePriceOptionAsync(Guid shoppingListId, Guid itemId, Guid priceOptionId, CancellationToken cancellationToken = default)
    {
        var priceOption = await _dbContext.ShoppingItemPriceOptions
            .Include(x => x.ShoppingItem)
            .FirstOrDefaultAsync(
                x => x.Id == priceOptionId &&
                     x.ShoppingItemId == itemId &&
                     x.ShoppingItem != null &&
                     x.ShoppingItem.ShoppingListId == shoppingListId,
                cancellationToken);

        if (priceOption is null)
        {
            return false;
        }

        _dbContext.ShoppingItemPriceOptions.Remove(priceOption);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var shoppingList = await _dbContext.ShoppingLists
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (shoppingList is null)
        {
            return false;
        }

        _dbContext.ShoppingLists.Remove(shoppingList);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> CompleteShoppingListAsync(
        Guid id,
        CompleteShoppingListRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var shoppingList = await _dbContext.ShoppingLists
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (shoppingList is null)
        {
            return false;
        }

        var completedItems = shoppingList.Items
            .Where(x => x.IsChecked || x.PurchasedQuantity > 0)
            .ToList();

        foreach (var item in completedItems)
        {
            if (string.IsNullOrWhiteSpace(item.Name))
            {
                continue;
            }

            var quantityToAdd = item.PurchasedQuantity > 0
                ? item.PurchasedQuantity
                : item.Quantity;

            if (quantityToAdd <= 0)
            {
                continue;
            }

            var inventoryItem = await _dbContext.InventoryItems
                .FirstOrDefaultAsync(
                    x => x.Name.ToLower() == item.Name.ToLower() && x.Unit == item.Unit,
                    cancellationToken);

            if (inventoryItem is null)
            {
                inventoryItem = new InventoryItem
                {
                    Name = item.Name.Trim(),
                    Quantity = quantityToAdd,
                    Unit = string.IsNullOrWhiteSpace(item.Unit) ? "Stk" : item.Unit.Trim(),
                    Notes = null
                };

                _dbContext.InventoryItems.Add(inventoryItem);
            }
            else
            {
                inventoryItem.Quantity += quantityToAdd;
            }
        }

        if (request?.CreateBudgetExpense == true)
        {
            if (request.ExpenseCategoryId is null || request.ExpenseCategoryId == Guid.Empty)
            {
                throw new ArgumentException("Für die Budget-Ausgabe ist eine Expense-Kategorie erforderlich.");
            }

            var category = await _dbContext.Categories
                .FirstOrDefaultAsync(x => x.Id == request.ExpenseCategoryId.Value, cancellationToken);

            if (category is null)
            {
                throw new InvalidOperationException("Die angegebene Budget-Kategorie wurde nicht gefunden.");
            }

            if (!string.Equals(category.Type, "Expense", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Für Einkäufe kann nur eine Expense-Kategorie verwendet werden.");
            }

            var totalAmount = shoppingList.Items.Sum(x => x.ActualTotalPrice ?? x.EstimatedTotalPrice ?? 0m);

            if (totalAmount <= 0)
            {
                throw new ArgumentException("Für die Budget-Ausgabe muss ein Gesamtpreis größer als 0 vorhanden sein.");
            }

            var transaction = new Transaction
            {
                Title = string.IsNullOrWhiteSpace(request.TransactionTitle)
                    ? $"Einkauf · {shoppingList.Name}"
                    : request.TransactionTitle.Trim(),
                Amount = -Math.Abs(totalAmount),
                Date = DateTime.SpecifyKind((request.TransactionDate ?? DateTime.UtcNow).Date, DateTimeKind.Utc),
                Note = "Automatisch aus Einkaufsliste erstellt",
                CategoryId = category.Id
            };

            _dbContext.Transactions.Add(transaction);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _shoppingListSyncService.SyncAsync(cancellationToken);
        return true;
    }

    private static void ValidateShoppingList(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Der Name der Einkaufsliste ist erforderlich.");
        }
    }

    private async Task<ShoppingItemDto?> GetShoppingItemAsync(Guid itemId, CancellationToken cancellationToken)
    {
        return await _dbContext.ShoppingItems
            .Include(x => x.PriceOptions)
            .Include(x => x.CatalogItem)
            .Where(x => x.Id == itemId)
            .Select(i => new ShoppingItemDto
            {
                Id = i.Id,
                Name = i.Name,
                RequiredQuantity = i.RequiredQuantity,
                InventoryQuantityUsed = i.InventoryQuantityUsed,
                Quantity = i.Quantity,
                PurchasedQuantity = i.PurchasedQuantity,
                Unit = i.Unit,
                IsChecked = i.IsChecked,
                EstimatedUnitPrice = i.EstimatedUnitPrice,
                EstimatedTotalPrice = i.EstimatedTotalPrice,
                ActualTotalPrice = i.ActualTotalPrice,
                SourceType = i.SourceType,
                CatalogItemId = i.CatalogItemId,
                CatalogItemName = i.CatalogItem != null ? i.CatalogItem.Name : null,
                PriceOptions = i.PriceOptions
                    .OrderBy(p => p.TotalPrice)
                    .Select(p => new ShoppingItemPriceOptionDto
                    {
                        Id = p.Id,
                        StoreName = p.StoreName,
                        ProductName = p.ProductName,
                        UnitPrice = p.UnitPrice,
                        TotalPrice = p.TotalPrice,
                        ProductUrl = p.ProductUrl,
                        IsAvailable = p.IsAvailable,
                        CheckedAt = p.CheckedAt,
                        ShoppingItemId = p.ShoppingItemId
                    })
                    .ToList(),
                ShoppingListId = i.ShoppingListId
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static ShoppingItem CreateItemEntity(CreateShoppingItemRequest item)
    {
        ValidateShoppingItem(item.Name, item.Quantity);

        return new ShoppingItem
        {
            Name = item.Name.Trim(),
            RequiredQuantity = item.RequiredQuantity,
            InventoryQuantityUsed = item.InventoryQuantityUsed,
            Quantity = item.Quantity,
            PurchasedQuantity = item.PurchasedQuantity,
            Unit = string.IsNullOrWhiteSpace(item.Unit) ? "Stk" : item.Unit.Trim(),
            IsChecked = false,
            EstimatedUnitPrice = item.EstimatedUnitPrice,
            EstimatedTotalPrice = item.EstimatedTotalPrice,
            ActualTotalPrice = item.ActualTotalPrice,
            SourceType = string.IsNullOrWhiteSpace(item.SourceType) ? "Manual" : item.SourceType.Trim(),
            CatalogItemId = item.CatalogItemId
        };
    }

    private static void ValidateShoppingItem(string name, decimal quantity)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Jeder Einkaufsposten benötigt einen Namen.");
        }

        if (quantity < 0)
        {
            throw new ArgumentException("Die Menge eines Einkaufspostens darf nicht negativ sein.");
        }
    }

    private static void ValidatePriceOption(string storeName, string productName, decimal unitPrice, decimal totalPrice)
    {
        if (string.IsNullOrWhiteSpace(storeName))
        {
            throw new ArgumentException("Der Händler ist erforderlich.");
        }

        if (string.IsNullOrWhiteSpace(productName))
        {
            throw new ArgumentException("Der Produktname ist erforderlich.");
        }

        if (unitPrice < 0 || totalPrice < 0)
        {
            throw new ArgumentException("Preise dürfen nicht negativ sein.");
        }
    }

    private static ShoppingItemPriceOptionDto MapPriceOption(ShoppingItemPriceOption priceOption)
    {
        return new ShoppingItemPriceOptionDto
        {
            Id = priceOption.Id,
            ShoppingItemId = priceOption.ShoppingItemId,
            StoreName = priceOption.StoreName,
            ProductName = priceOption.ProductName,
            UnitPrice = priceOption.UnitPrice,
            TotalPrice = priceOption.TotalPrice,
            ProductUrl = priceOption.ProductUrl,
            IsAvailable = priceOption.IsAvailable,
            CheckedAt = priceOption.CheckedAt
        };
    }

    private static Expression<Func<ShoppingList, ShoppingListDto>> MapToDto()
    {
        return x => new ShoppingListDto
        {
            Id = x.Id,
            Name = x.Name,
            CreatedAt = x.CreatedAt,
            Items = x.Items
                .OrderBy(i => i.Name)
                .Select(i => new ShoppingItemDto
                {
                    Id = i.Id,
                    Name = i.Name,
                    RequiredQuantity = i.RequiredQuantity,
                    InventoryQuantityUsed = i.InventoryQuantityUsed,
                    Quantity = i.Quantity,
                    PurchasedQuantity = i.PurchasedQuantity,
                    Unit = i.Unit,
                    IsChecked = i.IsChecked,
                    EstimatedUnitPrice = i.EstimatedUnitPrice,
                    EstimatedTotalPrice = i.EstimatedTotalPrice,
                    ActualTotalPrice = i.ActualTotalPrice,
                    SourceType = i.SourceType,
                    CatalogItemId = i.CatalogItemId,
                    CatalogItemName = i.CatalogItem != null ? i.CatalogItem.Name : null,
                    PriceOptions = i.PriceOptions
                        .OrderBy(p => p.TotalPrice)
                        .Select(p => new ShoppingItemPriceOptionDto
                        {
                            Id = p.Id,
                            StoreName = p.StoreName,
                            ProductName = p.ProductName,
                            UnitPrice = p.UnitPrice,
                            TotalPrice = p.TotalPrice,
                            ProductUrl = p.ProductUrl,
                            IsAvailable = p.IsAvailable,
                            CheckedAt = p.CheckedAt,
                            ShoppingItemId = p.ShoppingItemId
                        })
                        .ToList(),
                    ShoppingListId = i.ShoppingListId
                })
                .ToList()
        };
    }
}
