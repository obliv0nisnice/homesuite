using HomeSuite.Application.DTOs.MealPlans;
using HomeSuite.Application.Interfaces;
using HomeSuite.Domain.Entities;
using HomeSuite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HomeSuite.Infrastructure.Services;

public class MealPlanService : IMealPlanService
{   

private static string BuildIngredientKey(string name, string unit)
{
    return $"{name.Trim().ToLowerInvariant()}|{unit.Trim().ToLowerInvariant()}";
}

private sealed class AggregatedRequirement
{
    public string Name { get; set; } = string.Empty;
    public decimal RequiredQuantity { get; set; }
    public decimal InventoryQuantity { get; set; }
    public string Unit { get; set; } = string.Empty;
}

  
    public async Task<MealPlanWeekSummaryDto> GetWeekSummaryAsync(DateOnly weekStartDate, CancellationToken cancellationToken = default)
{
    var weekEndDate = weekStartDate.AddDays(6);

    var mealPlans = await _dbContext.MealPlans
        .Include(x => x.Recipe)
        .ThenInclude(x => x!.Ingredients)
        .Where(x => x.Date >= weekStartDate && x.Date <= weekEndDate)
        .ToListAsync(cancellationToken);

    var aggregatedRequirements = new Dictionary<string, AggregatedRequirement>(StringComparer.OrdinalIgnoreCase);

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
            var requiredQuantity = ingredient.Quantity * factor;

            if (aggregatedRequirements.TryGetValue(key, out var existing))
            {
                existing.RequiredQuantity += requiredQuantity;
            }
            else
            {
                aggregatedRequirements[key] = new AggregatedRequirement
                {
                    Name = normalizedName,
                    Unit = normalizedUnit,
                    RequiredQuantity = requiredQuantity
                };
            }
        }
    }

    var inventoryItems = await _dbContext.InventoryItems.ToListAsync(cancellationToken);

    foreach (var inventoryItem in inventoryItems)
    {
        var key = BuildIngredientKey(inventoryItem.Name.Trim(), inventoryItem.Unit.Trim());

        if (aggregatedRequirements.TryGetValue(key, out var requirement))
        {
            requirement.InventoryQuantity = inventoryItem.Quantity;
        }
    }

    var ingredients = aggregatedRequirements.Values
        .OrderBy(x => x.Name)
        .Select(x => new MealPlanWeekIngredientDto
        {
            Name = x.Name,
            RequiredQuantity = x.RequiredQuantity,
            InventoryQuantity = x.InventoryQuantity,
            MissingQuantity = Math.Max(0, x.RequiredQuantity - x.InventoryQuantity),
            Unit = x.Unit
        })
        .ToList();

    return new MealPlanWeekSummaryDto
    {
        WeekStartDate = weekStartDate,
        WeekEndDate = weekEndDate,
        Ingredients = ingredients
    };
}


    private readonly HomeSuiteDbContext _dbContext;

    public MealPlanService(HomeSuiteDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<MealPlanDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.MealPlans
            .Include(x => x.Recipe)
            .OrderBy(x => x.Date)
            .ThenBy(x => x.MealType)
            .Select(x => new MealPlanDto
            {
                Id = x.Id,
                Date = x.Date,
                MealType = x.MealType,
                Servings = x.Servings,
                Notes = x.Notes,
                IsCompleted = x.IsCompleted,
                RecipeId = x.RecipeId,
                RecipeName = x.Recipe != null ? x.Recipe.Name : string.Empty
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<MealPlanDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.MealPlans
            .Include(x => x.Recipe)
            .Where(x => x.Id == id)
            .Select(x => new MealPlanDto
            {
                Id = x.Id,
                Date = x.Date,
                MealType = x.MealType,
                Servings = x.Servings,
                Notes = x.Notes,
                IsCompleted = x.IsCompleted,
                RecipeId = x.RecipeId,
                RecipeName = x.Recipe != null ? x.Recipe.Name : string.Empty
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<MealPlanDto> CreateAsync(CreateMealPlanRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedMealType = ValidateAndNormalize(request.MealType, request.Servings, request.RecipeId);

        var recipe = await _dbContext.Recipes
            .FirstOrDefaultAsync(x => x.Id == request.RecipeId, cancellationToken);

        if (recipe is null)
        {
            throw new InvalidOperationException("Das Rezept existiert nicht.");
        }

        var mealPlan = new MealPlan
        {
            Date = request.Date,
            MealType = normalizedMealType,
            Servings = request.Servings,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            IsCompleted = false,
            RecipeId = request.RecipeId
        };

        _dbContext.MealPlans.Add(mealPlan);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new MealPlanDto
        {
            Id = mealPlan.Id,
            Date = mealPlan.Date,
            MealType = mealPlan.MealType,
            Servings = mealPlan.Servings,
            Notes = mealPlan.Notes,
            IsCompleted = mealPlan.IsCompleted,
            RecipeId = mealPlan.RecipeId,
            RecipeName = recipe.Name
        };
    }

    public async Task<MealPlanDto?> UpdateAsync(Guid id, UpdateMealPlanRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedMealType = ValidateAndNormalize(request.MealType, request.Servings, request.RecipeId);

        var mealPlan = await _dbContext.MealPlans
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (mealPlan is null)
        {
            return null;
        }

        var recipe = await _dbContext.Recipes
            .FirstOrDefaultAsync(x => x.Id == request.RecipeId, cancellationToken);

        if (recipe is null)
        {
            throw new InvalidOperationException("Das Rezept existiert nicht.");
        }

        mealPlan.Date = request.Date;
        mealPlan.MealType = normalizedMealType;
        mealPlan.Servings = request.Servings;
        mealPlan.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        mealPlan.RecipeId = request.RecipeId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new MealPlanDto
        {
            Id = mealPlan.Id,
            Date = mealPlan.Date,
            MealType = mealPlan.MealType,
            Servings = mealPlan.Servings,
            Notes = mealPlan.Notes,
            IsCompleted = mealPlan.IsCompleted,
            RecipeId = mealPlan.RecipeId,
            RecipeName = recipe.Name
        };
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var mealPlan = await _dbContext.MealPlans
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (mealPlan is null)
        {
            return false;
        }

        _dbContext.MealPlans.Remove(mealPlan);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task CompleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var mealPlan = await _dbContext.MealPlans
            .Include(x => x.Recipe)
            .ThenInclude(x => x!.Ingredients)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (mealPlan is null)
        {
            throw new InvalidOperationException("Meal Plan nicht gefunden.");
        }

        if (mealPlan.IsCompleted)
        {
            throw new InvalidOperationException("Meal Plan wurde bereits abgeschlossen.");
        }

        if (mealPlan.Recipe is null)
        {
            throw new InvalidOperationException("Zum Meal Plan wurde kein Rezept gefunden.");
        }

        var baseServings = mealPlan.Recipe.BaseServings <= 0 ? 1 : mealPlan.Recipe.BaseServings;
        var factor = mealPlan.Servings / (decimal)baseServings;

        foreach (var ingredient in mealPlan.Recipe.Ingredients)
        {
            var quantityToConsume = ingredient.Quantity * factor;

            var inventoryItem = await _dbContext.InventoryItems
                .FirstOrDefaultAsync(x =>
                    x.Name.ToLower() == ingredient.Name.ToLower() &&
                    x.Unit.ToLower() == ingredient.Unit.ToLower(),
                    cancellationToken);

            if (inventoryItem is null)
            {
                continue;
            }

            inventoryItem.Quantity -= quantityToConsume;

            if (inventoryItem.Quantity < 0)
            {
                inventoryItem.Quantity = 0;
            }
        }

        mealPlan.IsCompleted = true;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string ValidateAndNormalize(string mealType, int servings, Guid recipeId)
    {
        if (string.IsNullOrWhiteSpace(mealType))
        {
            throw new ArgumentException("Der Mahlzeitentyp ist erforderlich.");
        }

        if (servings <= 0)
        {
            throw new ArgumentException("Die Portionen müssen größer als 0 sein.");
        }

        if (recipeId == Guid.Empty)
        {
            throw new ArgumentException("Ein gültiges Rezept ist erforderlich.");
        }

        var normalizedMealType = mealType.Trim();

        if (normalizedMealType is not ("Breakfast" or "Lunch" or "Dinner" or "Snack"))
        {
            throw new ArgumentException("Der Mahlzeitentyp muss Breakfast, Lunch, Dinner oder Snack sein.");
        }

        return normalizedMealType;
    }
}
