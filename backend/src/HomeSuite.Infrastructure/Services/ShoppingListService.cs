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
            .OrderByDescending(x => x.CreatedAt)
            .Select(MapToDto())
            .ToListAsync(cancellationToken);
    }

    public async Task<ShoppingListDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ShoppingLists
            .Include(x => x.Items)
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
            Store = string.IsNullOrWhiteSpace(request.Store) ? null : request.Store.Trim(),
            EstimatedTotalPrice = request.EstimatedTotalPrice,
            ActualTotalPrice = request.ActualTotalPrice,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedAt = DateTime.UtcNow,
            CompletedAt = null
        };

        if (request.Items is not null)
        {
            foreach (var item in request.Items)
            {
                shoppingList.Items.Add(CreateItemEntity(item));
            }
        }

        _dbContext.ShoppingLists.Add(shoppingList);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(shoppingList.Id, cancellationToken)
            ?? throw new InvalidOperationException("Einkaufsliste konnte nicht geladen werden.");
    }

    public async Task<ShoppingListDto?> UpdateAsync(Guid id, UpdateShoppingListRequest request, CancellationToken cancellationToken = default)
    {
        ValidateShoppingList(request.Name);

        var shoppingList = await _dbContext.ShoppingLists
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (shoppingList is null)
        {
            return null;
        }

        shoppingList.Name = request.Name.Trim();
        shoppingList.Store = string.IsNullOrWhiteSpace(request.Store) ? null : request.Store.Trim();
        shoppingList.EstimatedTotalPrice = request.EstimatedTotalPrice;
        shoppingList.ActualTotalPrice = request.ActualTotalPrice;
        shoppingList.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        _dbContext.ShoppingListItems.RemoveRange(shoppingList.Items);
        shoppingList.Items.Clear();

        if (request.Items is not null)
        {
            foreach (var item in request.Items)
            {
                shoppingList.Items.Add(CreateItemEntity(item));
            }
        }

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

        if (shoppingList.CompletedAt is not null)
        {
            throw new InvalidOperationException("Die Einkaufsliste wurde bereits abgeschlossen.");
        }

        shoppingList.CompletedAt = DateTime.UtcNow;

        foreach (var item in shoppingList.Items.Where(x => x.Purchased))
        {
            if (string.IsNullOrWhiteSpace(item.Name))
            {
                continue;
            }

            var inventoryItem = await _dbContext.InventoryItems
                .FirstOrDefaultAsync(
                    x => x.Name.ToLower() == item.Name.ToLower(),
                    cancellationToken);

            if (inventoryItem is null)
            {
                inventoryItem = new InventoryItem
                {
                    Name = item.Name.Trim(),
                    Quantity = item.Quantity,
                    Category = "Einkauf",
                    Notes = null
                };

                _dbContext.InventoryItems.Add(inventoryItem);
            }
            else
            {
                inventoryItem.Quantity += item.Quantity;
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

            var totalAmount = shoppingList.ActualTotalPrice ?? shoppingList.EstimatedTotalPrice ?? 0m;

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
                Note = string.IsNullOrWhiteSpace(shoppingList.Store)
                    ? "Automatisch aus Einkaufsliste erstellt"
                    : $"Automatisch aus Einkaufsliste erstellt · {shoppingList.Store}",
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

    private static ShoppingListItem CreateItemEntity(ShoppingListItemInputDto item)
    {
        if (string.IsNullOrWhiteSpace(item.Name))
        {
            throw new ArgumentException("Jeder Einkaufsposten benötigt einen Namen.");
        }

        if (item.Quantity <= 0)
        {
            throw new ArgumentException("Die Menge eines Einkaufspostens muss größer als 0 sein.");
        }

        return new ShoppingListItem
        {
            Name = item.Name.Trim(),
            Quantity = item.Quantity,
            Unit = string.IsNullOrWhiteSpace(item.Unit) ? null : item.Unit.Trim(),
            Purchased = item.Purchased
        };
    }

    private static Expression<Func<ShoppingList, ShoppingListDto>> MapToDto()
    {
        return x => new ShoppingListDto
        {
            Id = x.Id,
            Name = x.Name,
            Store = x.Store,
            EstimatedTotalPrice = x.EstimatedTotalPrice,
            ActualTotalPrice = x.ActualTotalPrice,
            Notes = x.Notes,
            CreatedAt = x.CreatedAt,
            CompletedAt = x.CompletedAt,
            Items = x.Items
                .OrderBy(i => i.Name)
                .Select(i => new ShoppingListItemDto
                {
                    Id = i.Id,
                    Name = i.Name,
                    Quantity = i.Quantity,
                    Unit = i.Unit,
                    Purchased = i.Purchased
                })
                .ToList()
        };
    }
}
