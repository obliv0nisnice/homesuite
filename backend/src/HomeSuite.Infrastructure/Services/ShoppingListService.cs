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
                        Quantity = i.Quantity,
                        Unit = i.Unit,
                        IsChecked = i.IsChecked,
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
                        Quantity = i.Quantity,
                        Unit = i.Unit,
                        IsChecked = i.IsChecked,
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
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (shoppingList is null)
        {
            return null;
        }

        shoppingList.Name = request.Name.Trim();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapShoppingListDto(shoppingList);
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

        var item = new ShoppingItem
        {
            Name = request.Name.Trim(),
            Quantity = request.Quantity,
            Unit = request.Unit.Trim(),
            IsChecked = false,
            ShoppingListId = shoppingListId
        };

        _dbContext.ShoppingItems.Add(item);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapShoppingItemDto(item);
    }

    public async Task<ShoppingItemDto?> UpdateItemAsync(Guid shoppingListId, Guid itemId, UpdateShoppingItemRequest request, CancellationToken cancellationToken = default)
    {
        ValidateItem(request.Name, request.Quantity, request.Unit);

        var item = await _dbContext.ShoppingItems
            .FirstOrDefaultAsync(x => x.Id == itemId && x.ShoppingListId == shoppingListId, cancellationToken);

        if (item is null)
        {
            return null;
        }

        item.Name = request.Name.Trim();
        item.Quantity = request.Quantity;
        item.Unit = request.Unit.Trim();
        item.IsChecked = request.IsChecked;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapShoppingItemDto(item);
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

        MergeIngredientsIntoShoppingList(shoppingList, recipe.Ingredients);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapShoppingListDto(shoppingList);
    }

    public async Task<ShoppingListDto> AddMealPlanWeekToShoppingListAsync(
        Guid shoppingListId,
        DateOnly weekStartDate,
        CancellationToken cancellationToken = default)
    {
        var shoppingList = await _dbContext.ShoppingLists
            .Include(x => x.Items)
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

        foreach (var mealPlan in mealPlans)
        {
            if (mealPlan.Recipe is null)
            {
                continue;
            }

            var scaledIngredients = mealPlan.Recipe.Ingredients.Select(x => new RecipeIngredient
            {
                Name = x.Name,
                Unit = x.Unit,
                Quantity = x.Quantity * mealPlan.Servings,
                RecipeId = x.RecipeId
            });

            MergeIngredientsIntoShoppingList(shoppingList, scaledIngredients);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapShoppingListDto(shoppingList);
    }

    private static void MergeIngredientsIntoShoppingList(
        ShoppingList shoppingList,
        IEnumerable<RecipeIngredient> ingredients)
    {
        foreach (var ingredient in ingredients)
        {
            var normalizedName = ingredient.Name.Trim().ToLowerInvariant();
            var normalizedUnit = ingredient.Unit.Trim().ToLowerInvariant();

            var existingItem = shoppingList.Items.FirstOrDefault(x =>
                x.Name.ToLower() == normalizedName &&
                x.Unit.ToLower() == normalizedUnit);

            if (existingItem is not null)
            {
                existingItem.Quantity += ingredient.Quantity;
            }
            else
            {
                shoppingList.Items.Add(new ShoppingItem
                {
                    Name = ingredient.Name.Trim(),
                    Quantity = ingredient.Quantity,
                    Unit = ingredient.Unit.Trim(),
                    IsChecked = false,
                    ShoppingListId = shoppingList.Id
                });
            }
        }
    }

    private static ShoppingListDto MapShoppingListDto(ShoppingList shoppingList)
    {
        return new ShoppingListDto
        {
            Id = shoppingList.Id,
            Name = shoppingList.Name,
            CreatedAt = shoppingList.CreatedAt,
            Items = shoppingList.Items
                .OrderBy(x => x.Name)
                .Select(MapShoppingItemDto)
                .ToList()
        };
    }

    private static ShoppingItemDto MapShoppingItemDto(ShoppingItem item)
    {
        return new ShoppingItemDto
        {
            Id = item.Id,
            Name = item.Name,
            Quantity = item.Quantity,
            Unit = item.Unit,
            IsChecked = item.IsChecked,
            ShoppingListId = item.ShoppingListId
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
            throw new ArgumentException("Die Menge muss größer als 0 sein.");
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            throw new ArgumentException("Die Einheit ist erforderlich.");
        }
    }
}
