using HomeSuite.Application.DTOs.MealPlans;
using HomeSuite.Application.Interfaces;
using HomeSuite.Domain.Entities;
using HomeSuite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HomeSuite.Infrastructure.Services;

public class MealPlanService : IMealPlanService
{
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
