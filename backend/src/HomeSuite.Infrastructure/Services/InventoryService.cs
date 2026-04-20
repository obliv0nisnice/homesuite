using HomeSuite.Application.DTOs.Inventory;
using HomeSuite.Application.Interfaces;
using HomeSuite.Domain.Entities;
using HomeSuite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HomeSuite.Infrastructure.Services;

public class InventoryService : IInventoryService
{
    private readonly HomeSuiteDbContext _dbContext;
    private readonly UpcomingMealPlanShoppingListSyncService _shoppingListSyncService;

    public InventoryService(
        HomeSuiteDbContext dbContext,
        UpcomingMealPlanShoppingListSyncService shoppingListSyncService)
    {
        _dbContext = dbContext;
        _shoppingListSyncService = shoppingListSyncService;
    }

    public async Task<List<InventoryItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.InventoryItems
            .OrderBy(x => x.Name)
            .Select(x => new InventoryItemDto
            {
                Id = x.Id,
                Name = x.Name,
                Quantity = x.Quantity,
                Unit = x.Unit,
                Notes = x.Notes
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<InventoryItemDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.InventoryItems
            .Where(x => x.Id == id)
            .Select(x => new InventoryItemDto
            {
                Id = x.Id,
                Name = x.Name,
                Quantity = x.Quantity,
                Unit = x.Unit,
                Notes = x.Notes
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<InventoryItemDto> CreateAsync(CreateInventoryItemRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request.Name, request.Quantity, request.Unit);

        var item = new InventoryItem
        {
            Name = request.Name.Trim(),
            Quantity = request.Quantity,
            Unit = request.Unit.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        };

        _dbContext.InventoryItems.Add(item);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _shoppingListSyncService.SyncAsync(cancellationToken);

        return new InventoryItemDto
        {
            Id = item.Id,
            Name = item.Name,
            Quantity = item.Quantity,
            Unit = item.Unit,
            Notes = item.Notes
        };
    }

    public async Task<InventoryItemDto?> UpdateAsync(Guid id, UpdateInventoryItemRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request.Name, request.Quantity, request.Unit);

        var item = await _dbContext.InventoryItems
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (item is null)
        {
            return null;
        }

        item.Name = request.Name.Trim();
        item.Quantity = request.Quantity;
        item.Unit = request.Unit.Trim();
        item.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _shoppingListSyncService.SyncAsync(cancellationToken);

        return new InventoryItemDto
        {
            Id = item.Id,
            Name = item.Name,
            Quantity = item.Quantity,
            Unit = item.Unit,
            Notes = item.Notes
        };
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.InventoryItems
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (item is null)
        {
            return false;
        }

        _dbContext.InventoryItems.Remove(item);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _shoppingListSyncService.SyncAsync(cancellationToken);

        return true;
    }

    private static void ValidateRequest(string name, decimal quantity, string unit)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Der Name ist erforderlich.");
        }

        if (quantity < 0)
        {
            throw new ArgumentException("Die Menge darf nicht negativ sein.");
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            throw new ArgumentException("Die Einheit ist erforderlich.");
        }
    }
}
