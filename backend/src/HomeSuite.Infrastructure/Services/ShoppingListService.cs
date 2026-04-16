using HomeSuite.Application.DTOs.ShoppingLists;
using HomeSuite.Application.Interfaces;
using HomeSuite.Domain.Entities;
using HomeSuite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HomeSuite.Infrastructure.Services;

public class ShoppingListService : IShoppingListService
{
    private readonly HomeSuiteDbContext _dbContext;

    public ShoppingListService(HomeSuiteDbContext dbContext)
    {
        _dbContext = dbContext;
    }
public async Task<List<ShoppingListStoreSummaryDto>> GetStoreSummariesAsync(Guid shoppingListId, CancellationToken cancellationToken = default)
{
    var shoppingList = await _dbContext.ShoppingLists
        .Include(x => x.Items)
        .ThenInclude(x => x.PriceOptions)
        .FirstOrDefaultAsync(x => x.Id == shoppingListId, cancellationToken);

    if (shoppingList is null)
    {
        throw new InvalidOperationException("Die Einkaufsliste existiert nicht.");
    }

    var relevantItems = shoppingList.Items
        .Where(x => x.Quantity > 0)
        .ToList();

    if (relevantItems.Count == 0)
    {
        return [];
    }

    var storeMap = new Dictionary<string, ShoppingListStoreSummaryDto>(StringComparer.OrdinalIgnoreCase);

    foreach (var item in relevantItems)
    {
        var availableOptions = item.PriceOptions
            .Where(x => x.IsAvailable)
            .ToList();

        foreach (var option in availableOptions)
        {
            if (!storeMap.TryGetValue(option.StoreName, out var summary))
            {
                summary = new ShoppingListStoreSummaryDto
                {
                    StoreName = option.StoreName,
                    TotalEstimatedPrice = 0,
                    CoveredItemsCount = 0,
                    TotalItemsCount = relevantItems.Count,
                    IsComplete = false,
                    IsBestOption = false
                };

                storeMap[option.StoreName] = summary;
            }

            summary.TotalEstimatedPrice += option.TotalPrice;
            summary.CoveredItemsCount += 1;
        }
    }

    var summaries = storeMap.Values
        .Select(x =>
        {
            x.IsComplete = x.CoveredItemsCount == x.TotalItemsCount;
            return x;
        })
        .OrderBy(x => x.TotalEstimatedPrice)
        .ThenByDescending(x => x.CoveredItemsCount)
        .ToList();

    var bestComplete = summaries
        .Where(x => x.IsComplete)
        .OrderBy(x => x.TotalEstimatedPrice)
        .FirstOrDefault();

    if (bestComplete is not null)
    {
        bestComplete.IsBestOption = true;
    }

    return summaries;
}
    public async Task<List<ShoppingListDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.ShoppingLists
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ShoppingListDto
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
                            .OrderBy(p => p.StoreName)
                            .Select(p => new ShoppingItemPriceOptionDto
                            {
                                Id = p.Id,
                                ShoppingItemId = p.ShoppingItemId,
                                StoreName = p.StoreName,
                                ProductName = p.ProductName,
                                UnitPrice = p.UnitPrice,
                                TotalPrice = p.TotalPrice,
                                ProductUrl = p.ProductUrl,
                                IsAvailable = p.IsAvailable,
                                CheckedAt = p.CheckedAt
                            })
                            .ToList(),
                        ShoppingListId = i.ShoppingListId
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ShoppingListDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ShoppingLists
            .Where(x => x.Id == id)
            .Select(x => new ShoppingListDto
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
                            .OrderBy(p => p.StoreName)
                            .Select(p => new ShoppingItemPriceOptionDto
                            {
                                Id = p.Id,
                                ShoppingItemId = p.ShoppingItemId,
                                StoreName = p.StoreName,
                                ProductName = p.ProductName,
                                UnitPrice = p.UnitPrice,
                                TotalPrice = p.TotalPrice,
                                ProductUrl = p.ProductUrl,
                                IsAvailable = p.IsAvailable,
                                CheckedAt = p.CheckedAt
                            })
                            .ToList(),
                        ShoppingListId = i.ShoppingListId
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ShoppingListDto> CreateAsync(CreateShoppingListRequest request, CancellationToken cancellationToken = default)
    {
        ValidateListName(request.Name);

        var shoppingList = new ShoppingList
        {
            Name = request.Name.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ShoppingLists.Add(shoppingList);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ShoppingListDto
        {
            Id = shoppingList.Id,
            Name = shoppingList.Name,
            CreatedAt = shoppingList.CreatedAt,
            Items = []
        };
    }

    public async Task<ShoppingListDto?> UpdateAsync(Guid id, UpdateShoppingListRequest request, CancellationToken cancellationToken = default)
    {
        ValidateListName(request.Name);

        var shoppingList = await _dbContext.ShoppingLists
            .Include(x => x.Items)
            .ThenInclude(x => x.CatalogItem)
            .Include(x => x.Items)
            .ThenInclude(x => x.PriceOptions)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (shoppingList is null)
        {
            return null;
        }

        shoppingList.Name = request.Name.Trim();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapShoppingList(shoppingList);
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

    public async Task<ShoppingItemDto> AddItemAsync(Guid shoppingListId, CreateShoppingItemRequest request, CancellationToken cancellationToken = default)
    {
        ValidateItem(request.Name, request.Quantity, request.Unit);

        var shoppingListExists = await _dbContext.ShoppingLists
            .AnyAsync(x => x.Id == shoppingListId, cancellationToken);

        if (!shoppingListExists)
        {
            throw new InvalidOperationException("Die Einkaufsliste existiert nicht.");
        }

        CatalogItem? catalogItem = null;

        if (request.CatalogItemId.HasValue)
        {
            catalogItem = await _dbContext.CatalogItems
                .FirstOrDefaultAsync(x => x.Id == request.CatalogItemId.Value, cancellationToken);

            if (catalogItem is null)
            {
                throw new InvalidOperationException("Der Katalogeintrag existiert nicht.");
            }
        }

        var purchasedQuantity = request.PurchasedQuantity < 0 ? 0 : request.PurchasedQuantity;

        var item = new ShoppingItem
        {
            Name = request.Name.Trim(),
            RequiredQuantity = request.RequiredQuantity > 0 ? request.RequiredQuantity : request.Quantity,
            InventoryQuantityUsed = request.InventoryQuantityUsed < 0 ? 0 : request.InventoryQuantityUsed,
            Quantity = request.Quantity,
            PurchasedQuantity = purchasedQuantity,
            Unit = request.Unit.Trim(),
            IsChecked = false,
            EstimatedUnitPrice = request.EstimatedUnitPrice,
            EstimatedTotalPrice = request.EstimatedTotalPrice,
            ActualTotalPrice = request.ActualTotalPrice,
            SourceType = string.IsNullOrWhiteSpace(request.SourceType) ? "Manual" : request.SourceType.Trim(),
            CatalogItemId = request.CatalogItemId,
            ShoppingListId = shoppingListId
        };

        _dbContext.ShoppingItems.Add(item);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ShoppingItemDto
        {
            Id = item.Id,
            Name = item.Name,
            RequiredQuantity = item.RequiredQuantity,
            InventoryQuantityUsed = item.InventoryQuantityUsed,
            Quantity = item.Quantity,
            PurchasedQuantity = item.PurchasedQuantity,
            Unit = item.Unit,
            IsChecked = item.IsChecked,
            EstimatedUnitPrice = item.EstimatedUnitPrice,
            EstimatedTotalPrice = item.EstimatedTotalPrice,
            ActualTotalPrice = item.ActualTotalPrice,
            SourceType = item.SourceType,
            CatalogItemId = item.CatalogItemId,
            CatalogItemName = catalogItem?.Name,
            PriceOptions = [],
            ShoppingListId = item.ShoppingListId
        };
    }

    public async Task<ShoppingItemDto?> UpdateItemAsync(Guid shoppingListId, Guid itemId, UpdateShoppingItemRequest request, CancellationToken cancellationToken = default)
    {
        ValidateItem(request.Name, request.Quantity, request.Unit);

        var item = await _dbContext.ShoppingItems
            .Include(x => x.CatalogItem)
            .Include(x => x.PriceOptions)
            .FirstOrDefaultAsync(x => x.Id == itemId && x.ShoppingListId == shoppingListId, cancellationToken);

        if (item is null)
        {
            return null;
        }

        CatalogItem? catalogItem = null;

        if (request.CatalogItemId.HasValue)
        {
            catalogItem = await _dbContext.CatalogItems
                .FirstOrDefaultAsync(x => x.Id == request.CatalogItemId.Value, cancellationToken);

            if (catalogItem is null)
            {
                throw new InvalidOperationException("Der Katalogeintrag existiert nicht.");
            }
        }

        item.Name = request.Name.Trim();
        item.RequiredQuantity = request.RequiredQuantity > 0 ? request.RequiredQuantity : request.Quantity;
        item.InventoryQuantityUsed = request.InventoryQuantityUsed < 0 ? 0 : request.InventoryQuantityUsed;
        item.Quantity = request.Quantity;
        item.PurchasedQuantity = request.PurchasedQuantity < 0 ? 0 : request.PurchasedQuantity;
        item.Unit = request.Unit.Trim();
        item.IsChecked = request.IsChecked;
        item.EstimatedUnitPrice = request.EstimatedUnitPrice;
        item.EstimatedTotalPrice = request.EstimatedTotalPrice;
        item.ActualTotalPrice = request.ActualTotalPrice;
        item.SourceType = string.IsNullOrWhiteSpace(request.SourceType) ? "Manual" : request.SourceType.Trim();
        item.CatalogItemId = request.CatalogItemId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ShoppingItemDto
        {
            Id = item.Id,
            Name = item.Name,
            RequiredQuantity = item.RequiredQuantity,
            InventoryQuantityUsed = item.InventoryQuantityUsed,
            Quantity = item.Quantity,
            PurchasedQuantity = item.PurchasedQuantity,
            Unit = item.Unit,
            IsChecked = item.IsChecked,
            EstimatedUnitPrice = item.EstimatedUnitPrice,
            EstimatedTotalPrice = item.EstimatedTotalPrice,
            ActualTotalPrice = item.ActualTotalPrice,
            SourceType = item.SourceType,
            CatalogItemId = item.CatalogItemId,
            CatalogItemName = catalogItem?.Name,
            PriceOptions = item.PriceOptions
                .OrderBy(p => p.StoreName)
                .Select(p => new ShoppingItemPriceOptionDto
                {
                    Id = p.Id,
                    ShoppingItemId = p.ShoppingItemId,
                    StoreName = p.StoreName,
                    ProductName = p.ProductName,
                    UnitPrice = p.UnitPrice,
                    TotalPrice = p.TotalPrice,
                    ProductUrl = p.ProductUrl,
                    IsAvailable = p.IsAvailable,
                    CheckedAt = p.CheckedAt
                })
                .ToList(),
            ShoppingListId = item.ShoppingListId
        };
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

    public async Task<ShoppingListDto> AddRecipeToShoppingListAsync(Guid shoppingListId, Guid recipeId, CancellationToken cancellationToken = default)
    {
        var shoppingList = await _dbContext.ShoppingLists
            .Include(x => x.Items)
            .ThenInclude(x => x.CatalogItem)
            .Include(x => x.Items)
            .ThenInclude(x => x.PriceOptions)
            .FirstOrDefaultAsync(x => x.Id == shoppingListId, cancellationToken);

        if (shoppingList is null)
        {
            throw new InvalidOperationException("Die Einkaufsliste existiert nicht.");
        }

        var recipe = await _dbContext.Recipes
            .Include(x => x.Ingredients)
            .FirstOrDefaultAsync(x => x.Id == recipeId, cancellationToken);

        if (recipe is null)
        {
            throw new InvalidOperationException("Das Rezept existiert nicht.");
        }

        MergeRecipeIngredientsIntoShoppingList(shoppingList, recipe.Ingredients, 1m);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapShoppingList(shoppingList);
    }

    public async Task<ShoppingListDto> AddMealPlanWeekToShoppingListAsync(Guid shoppingListId, DateOnly weekStartDate, CancellationToken cancellationToken = default)
    {
        var shoppingList = await _dbContext.ShoppingLists
            .Include(x => x.Items)
            .ThenInclude(x => x.CatalogItem)
            .Include(x => x.Items)
            .ThenInclude(x => x.PriceOptions)
            .FirstOrDefaultAsync(x => x.Id == shoppingListId, cancellationToken);

        if (shoppingList is null)
        {
            throw new InvalidOperationException("Die Einkaufsliste existiert nicht.");
        }

        var weekEndDate = weekStartDate.AddDays(6);

        var mealPlans = await _dbContext.MealPlans
            .Include(x => x.Recipe)
            .ThenInclude(x => x!.Ingredients)
            .Where(x => x.Date >= weekStartDate && x.Date <= weekEndDate)
            .ToListAsync(cancellationToken);

        if (mealPlans.Count == 0)
        {
            throw new InvalidOperationException("Für diese Woche existieren keine Meal Plans.");
        }

        var aggregatedRequirements = new Dictionary<string, AggregatedIngredient>(StringComparer.OrdinalIgnoreCase);

        foreach (var mealPlan in mealPlans)
        {
            if (mealPlan.Recipe?.Ingredients is null)
            {
                continue;
            }

            var baseServings = mealPlan.Recipe.BaseServings <= 0 ? 1 : mealPlan.Recipe.BaseServings;
            var factor = mealPlan.Servings / (decimal)baseServings;

            foreach (var ingredient in mealPlan.Recipe.Ingredients)
            {
                var normalizedName = ingredient.Name.Trim();
                var normalizedUnit = ingredient.Unit.Trim();
                var key = BuildIngredientKey(normalizedName, normalizedUnit);
                var quantityToAdd = ingredient.Quantity * factor;

                if (aggregatedRequirements.TryGetValue(key, out var existing))
                {
                    existing.RequiredQuantity += quantityToAdd;
                    existing.Quantity += quantityToAdd;
                }
                else
                {
                    aggregatedRequirements[key] = new AggregatedIngredient
                    {
                        Name = normalizedName,
                        Unit = normalizedUnit,
                        RequiredQuantity = quantityToAdd,
                        InventoryQuantityUsed = 0,
                        Quantity = quantityToAdd
                    };
                }
            }
        }

        if (aggregatedRequirements.Count == 0)
        {
            throw new InvalidOperationException("Für diese Woche wurden keine Zutaten gefunden.");
        }

        var inventoryItems = await _dbContext.InventoryItems.ToListAsync(cancellationToken);

        foreach (var inventoryItem in inventoryItems)
        {
            var key = BuildIngredientKey(inventoryItem.Name.Trim(), inventoryItem.Unit.Trim());

            if (!aggregatedRequirements.TryGetValue(key, out var required))
            {
                continue;
            }

            var usedFromInventory = Math.Min(required.Quantity, inventoryItem.Quantity);

            required.InventoryQuantityUsed += usedFromInventory;
            required.Quantity -= usedFromInventory;

            if (required.Quantity <= 0)
            {
                aggregatedRequirements.Remove(key);
            }
        }

        if (aggregatedRequirements.Count == 0)
        {
            throw new InvalidOperationException("Alle benötigten Zutaten sind bereits im Inventar vorhanden.");
        }

        foreach (var missing in aggregatedRequirements.Values)
        {
            var existingShoppingItem = shoppingList.Items.FirstOrDefault(x =>
                string.Equals(x.Name.Trim(), missing.Name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Unit.Trim(), missing.Unit, StringComparison.OrdinalIgnoreCase));

            if (existingShoppingItem is not null)
            {
                existingShoppingItem.RequiredQuantity += missing.RequiredQuantity;
                existingShoppingItem.InventoryQuantityUsed += missing.InventoryQuantityUsed;
                existingShoppingItem.Quantity += missing.Quantity;
            }
            else
            {
                var catalogItem = await FindCatalogItemAsync(missing.Name, missing.Unit, cancellationToken);

                shoppingList.Items.Add(new ShoppingItem
                {
                    Name = missing.Name,
                    RequiredQuantity = missing.RequiredQuantity,
                    InventoryQuantityUsed = missing.InventoryQuantityUsed,
                    Quantity = missing.Quantity,
                    PurchasedQuantity = 0,
                    Unit = missing.Unit,
                    IsChecked = false,
                    EstimatedUnitPrice = null,
                    EstimatedTotalPrice = null,
                    ActualTotalPrice = null,
                    SourceType = "MealPlanWeek",
                    CatalogItemId = catalogItem?.Id,
                    ShoppingListId = shoppingList.Id
                });
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapShoppingList(shoppingList);
    }

    public async Task CompleteShoppingListAsync(Guid shoppingListId, CancellationToken cancellationToken = default)
    {
        var shoppingList = await _dbContext.ShoppingLists
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == shoppingListId, cancellationToken);

        if (shoppingList is null)
        {
            throw new InvalidOperationException("Die Einkaufsliste existiert nicht.");
        }

        var purchasedItems = shoppingList.Items
            .Where(x => x.IsChecked)
            .ToList();

        foreach (var item in purchasedItems)
        {
            var quantityToStore = item.PurchasedQuantity > 0 ? item.PurchasedQuantity : item.Quantity;

            if (quantityToStore <= 0)
            {
                continue;
            }

            var existingInventoryItem = await _dbContext.InventoryItems
                .FirstOrDefaultAsync(x =>
                    x.Name.ToLower() == item.Name.ToLower() &&
                    x.Unit.ToLower() == item.Unit.ToLower(),
                    cancellationToken);

            if (existingInventoryItem is not null)
            {
                existingInventoryItem.Quantity += quantityToStore;
            }
            else
            {
                _dbContext.InventoryItems.Add(new InventoryItem
                {
                    Name = item.Name,
                    Quantity = quantityToStore,
                    Unit = item.Unit
                });
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<ShoppingItemPriceOptionDto>> GetPriceOptionsAsync(Guid shoppingListId, Guid shoppingItemId, CancellationToken cancellationToken = default)
    {
        var shoppingItemExists = await _dbContext.ShoppingItems
            .AnyAsync(x => x.Id == shoppingItemId && x.ShoppingListId == shoppingListId, cancellationToken);

        if (!shoppingItemExists)
        {
            throw new InvalidOperationException("Der Einkaufslistenartikel existiert nicht.");
        }

        return await _dbContext.ShoppingItemPriceOptions
            .Where(x => x.ShoppingItemId == shoppingItemId)
            .OrderBy(x => x.StoreName)
            .Select(x => new ShoppingItemPriceOptionDto
            {
                Id = x.Id,
                ShoppingItemId = x.ShoppingItemId,
                StoreName = x.StoreName,
                ProductName = x.ProductName,
                UnitPrice = x.UnitPrice,
                TotalPrice = x.TotalPrice,
                ProductUrl = x.ProductUrl,
                IsAvailable = x.IsAvailable,
                CheckedAt = x.CheckedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ShoppingItemPriceOptionDto> AddPriceOptionAsync(Guid shoppingListId, Guid shoppingItemId, CreateShoppingItemPriceOptionRequest request, CancellationToken cancellationToken = default)
    {
        ValidatePriceOption(request.StoreName, request.ProductName, request.UnitPrice, request.TotalPrice);

        var shoppingItem = await _dbContext.ShoppingItems
            .FirstOrDefaultAsync(x => x.Id == shoppingItemId && x.ShoppingListId == shoppingListId, cancellationToken);

        if (shoppingItem is null)
        {
            throw new InvalidOperationException("Der Einkaufslistenartikel existiert nicht.");
        }

        var option = new ShoppingItemPriceOption
        {
            ShoppingItemId = shoppingItemId,
            StoreName = request.StoreName.Trim(),
            ProductName = request.ProductName.Trim(),
            UnitPrice = request.UnitPrice,
            TotalPrice = request.TotalPrice,
            ProductUrl = string.IsNullOrWhiteSpace(request.ProductUrl) ? null : request.ProductUrl.Trim(),
            IsAvailable = request.IsAvailable,
            CheckedAt = request.CheckedAt ?? DateTime.UtcNow
        };

        _dbContext.ShoppingItemPriceOptions.Add(option);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ShoppingItemPriceOptionDto
        {
            Id = option.Id,
            ShoppingItemId = option.ShoppingItemId,
            StoreName = option.StoreName,
            ProductName = option.ProductName,
            UnitPrice = option.UnitPrice,
            TotalPrice = option.TotalPrice,
            ProductUrl = option.ProductUrl,
            IsAvailable = option.IsAvailable,
            CheckedAt = option.CheckedAt
        };
    }

    public async Task<ShoppingItemPriceOptionDto?> UpdatePriceOptionAsync(Guid shoppingListId, Guid shoppingItemId, Guid priceOptionId, UpdateShoppingItemPriceOptionRequest request, CancellationToken cancellationToken = default)
    {
        ValidatePriceOption(request.StoreName, request.ProductName, request.UnitPrice, request.TotalPrice);

        var option = await _dbContext.ShoppingItemPriceOptions
            .Include(x => x.ShoppingItem)
            .FirstOrDefaultAsync(
                x => x.Id == priceOptionId &&
                     x.ShoppingItemId == shoppingItemId &&
                     x.ShoppingItem != null &&
                     x.ShoppingItem.ShoppingListId == shoppingListId,
                cancellationToken);

        if (option is null)
        {
            return null;
        }

        option.StoreName = request.StoreName.Trim();
        option.ProductName = request.ProductName.Trim();
        option.UnitPrice = request.UnitPrice;
        option.TotalPrice = request.TotalPrice;
        option.ProductUrl = string.IsNullOrWhiteSpace(request.ProductUrl) ? null : request.ProductUrl.Trim();
        option.IsAvailable = request.IsAvailable;
        option.CheckedAt = request.CheckedAt ?? DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ShoppingItemPriceOptionDto
        {
            Id = option.Id,
            ShoppingItemId = option.ShoppingItemId,
            StoreName = option.StoreName,
            ProductName = option.ProductName,
            UnitPrice = option.UnitPrice,
            TotalPrice = option.TotalPrice,
            ProductUrl = option.ProductUrl,
            IsAvailable = option.IsAvailable,
            CheckedAt = option.CheckedAt
        };
    }

    public async Task<bool> DeletePriceOptionAsync(Guid shoppingListId, Guid shoppingItemId, Guid priceOptionId, CancellationToken cancellationToken = default)
    {
        var option = await _dbContext.ShoppingItemPriceOptions
            .Include(x => x.ShoppingItem)
            .FirstOrDefaultAsync(
                x => x.Id == priceOptionId &&
                     x.ShoppingItemId == shoppingItemId &&
                     x.ShoppingItem != null &&
                     x.ShoppingItem.ShoppingListId == shoppingListId,
                cancellationToken);

        if (option is null)
        {
            return false;
        }

        _dbContext.ShoppingItemPriceOptions.Remove(option);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task<CatalogItem?> FindCatalogItemAsync(string name, string unit, CancellationToken cancellationToken)
    {
        var normalizedName = name.Trim().ToLowerInvariant();
        var normalizedUnit = unit.Trim().ToLowerInvariant();

        return await _dbContext.CatalogItems
            .FirstOrDefaultAsync(
                x => x.Name.ToLower() == normalizedName && x.DefaultUnit.ToLower() == normalizedUnit,
                cancellationToken);
    }

    private static void MergeRecipeIngredientsIntoShoppingList(
        ShoppingList shoppingList,
        IEnumerable<RecipeIngredient> ingredients,
        decimal factor)
    {
        foreach (var ingredient in ingredients)
        {
            var quantityToAdd = ingredient.Quantity * factor;

            var existingItem = shoppingList.Items.FirstOrDefault(x =>
                string.Equals(x.Name.Trim(), ingredient.Name.Trim(), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Unit.Trim(), ingredient.Unit.Trim(), StringComparison.OrdinalIgnoreCase));

            if (existingItem is not null)
            {
                existingItem.RequiredQuantity += quantityToAdd;
                existingItem.Quantity += quantityToAdd;
            }
            else
            {
                shoppingList.Items.Add(new ShoppingItem
                {
                    Name = ingredient.Name.Trim(),
                    RequiredQuantity = quantityToAdd,
                    InventoryQuantityUsed = 0,
                    Quantity = quantityToAdd,
                    PurchasedQuantity = 0,
                    Unit = ingredient.Unit.Trim(),
                    IsChecked = false,
                    EstimatedUnitPrice = null,
                    EstimatedTotalPrice = null,
                    ActualTotalPrice = null,
                    SourceType = "Recipe",
                    CatalogItemId = null,
                    ShoppingListId = shoppingList.Id
                });
            }
        }
    }

    private static string BuildIngredientKey(string name, string unit)
    {
        return $"{name.Trim().ToLowerInvariant()}|{unit.Trim().ToLowerInvariant()}";
    }

    private static ShoppingListDto MapShoppingList(ShoppingList shoppingList)
    {
        return new ShoppingListDto
        {
            Id = shoppingList.Id,
            Name = shoppingList.Name,
            CreatedAt = shoppingList.CreatedAt,
            Items = shoppingList.Items
                .OrderBy(x => x.Name)
                .Select(x => new ShoppingItemDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    RequiredQuantity = x.RequiredQuantity,
                    InventoryQuantityUsed = x.InventoryQuantityUsed,
                    Quantity = x.Quantity,
                    PurchasedQuantity = x.PurchasedQuantity,
                    Unit = x.Unit,
                    IsChecked = x.IsChecked,
                    EstimatedUnitPrice = x.EstimatedUnitPrice,
                    EstimatedTotalPrice = x.EstimatedTotalPrice,
                    ActualTotalPrice = x.ActualTotalPrice,
                    SourceType = x.SourceType,
                    CatalogItemId = x.CatalogItemId,
                    CatalogItemName = x.CatalogItem != null ? x.CatalogItem.Name : null,
                    PriceOptions = x.PriceOptions
                        .OrderBy(p => p.StoreName)
                        .Select(p => new ShoppingItemPriceOptionDto
                        {
                            Id = p.Id,
                            ShoppingItemId = p.ShoppingItemId,
                            StoreName = p.StoreName,
                            ProductName = p.ProductName,
                            UnitPrice = p.UnitPrice,
                            TotalPrice = p.TotalPrice,
                            ProductUrl = p.ProductUrl,
                            IsAvailable = p.IsAvailable,
                            CheckedAt = p.CheckedAt
                        })
                        .ToList(),
                    ShoppingListId = x.ShoppingListId
                })
                .ToList()
        };
    }

    private static void ValidateListName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Der Name der Einkaufsliste ist erforderlich.");
        }
    }

    private static void ValidateItem(string name, decimal quantity, string unit)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Der Name des Artikels ist erforderlich.");
        }

        if (quantity <= 0)
        {
            throw new ArgumentException("Die geplante Menge muss größer als 0 sein.");
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            throw new ArgumentException("Die Einheit ist erforderlich.");
        }
    }

    private static void ValidatePriceOption(string storeName, string productName, decimal unitPrice, decimal totalPrice)
    {
        if (string.IsNullOrWhiteSpace(storeName))
        {
            throw new ArgumentException("Der Händlername ist erforderlich.");
        }

        if (string.IsNullOrWhiteSpace(productName))
        {
            throw new ArgumentException("Der Produktname ist erforderlich.");
        }

        if (unitPrice < 0)
        {
            throw new ArgumentException("Der Einzelpreis darf nicht negativ sein.");
        }

        if (totalPrice < 0)
        {
            throw new ArgumentException("Der Gesamtpreis darf nicht negativ sein.");
        }
    }

    private sealed class AggregatedIngredient
    {
        public string Name { get; set; } = string.Empty;
        public decimal RequiredQuantity { get; set; }
        public decimal InventoryQuantityUsed { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
    }
}
