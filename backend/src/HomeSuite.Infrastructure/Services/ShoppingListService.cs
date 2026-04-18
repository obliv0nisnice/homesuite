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

    public ShoppingListService(HomeSuiteDbContext dbContext)
    {
        _dbContext = dbContext;
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
        return true;
    }

    private static void ValidateShoppingList(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Der Name der Einkaufsliste ist erforderlich.");
        }
    }

    private static ShoppingItem CreateItemEntity(CreateShoppingItemRequest item)
    {
        if (string.IsNullOrWhiteSpace(item.Name))
        {
            throw new ArgumentException("Jeder Einkaufsposten benötigt einen Namen.");
        }

        if (item.Quantity < 0)
        {
            throw new ArgumentException("Die Menge eines Einkaufspostens darf nicht negativ sein.");
        }

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
