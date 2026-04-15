using HomeSuite.Application.DTOs.Recipes;
using HomeSuite.Application.Interfaces;
using HomeSuite.Domain.Entities;
using HomeSuite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HomeSuite.Infrastructure.Services;

public class RecipeService : IRecipeService
{
    private readonly HomeSuiteDbContext _dbContext;

    public RecipeService(HomeSuiteDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<RecipeDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Recipes
            .OrderBy(x => x.Name)
            .Select(x => new RecipeDto
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                Ingredients = x.Ingredients
                    .OrderBy(i => i.Name)
                    .Select(i => new RecipeIngredientDto
                    {
                        Id = i.Id,
                        Name = i.Name,
                        Quantity = i.Quantity,
                        Unit = i.Unit,
                        RecipeId = i.RecipeId
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<RecipeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Recipes
            .Where(x => x.Id == id)
            .Select(x => new RecipeDto
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                Ingredients = x.Ingredients
                    .OrderBy(i => i.Name)
                    .Select(i => new RecipeIngredientDto
                    {
                        Id = i.Id,
                        Name = i.Name,
                        Quantity = i.Quantity,
                        Unit = i.Unit,
                        RecipeId = i.RecipeId
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<RecipeDto> CreateAsync(CreateRecipeRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRecipe(request.Name);

        var recipe = new Recipe
        {
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim()
        };

        _dbContext.Recipes.Add(recipe);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RecipeDto
        {
            Id = recipe.Id,
            Name = recipe.Name,
            Description = recipe.Description,
            Ingredients = []
        };
    }

    public async Task<RecipeDto?> UpdateAsync(Guid id, UpdateRecipeRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRecipe(request.Name);

        var recipe = await _dbContext.Recipes
            .Include(x => x.Ingredients)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (recipe is null)
        {
            return null;
        }

        recipe.Name = request.Name.Trim();
        recipe.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RecipeDto
        {
            Id = recipe.Id,
            Name = recipe.Name,
            Description = recipe.Description,
            Ingredients = recipe.Ingredients
                .OrderBy(x => x.Name)
                .Select(x => new RecipeIngredientDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    Quantity = x.Quantity,
                    Unit = x.Unit,
                    RecipeId = x.RecipeId
                })
                .ToList()
        };
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var recipe = await _dbContext.Recipes
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (recipe is null)
        {
            return false;
        }

        _dbContext.Recipes.Remove(recipe);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<RecipeIngredientDto> AddIngredientAsync(Guid recipeId, CreateRecipeIngredientRequest request, CancellationToken cancellationToken = default)
    {
        ValidateIngredient(request.Name, request.Quantity, request.Unit);

        var recipeExists = await _dbContext.Recipes
            .AnyAsync(x => x.Id == recipeId, cancellationToken);

        if (!recipeExists)
        {
            throw new InvalidOperationException("Das Rezept existiert nicht.");
        }

        var ingredient = new RecipeIngredient
        {
            Name = request.Name.Trim(),
            Quantity = request.Quantity,
            Unit = request.Unit.Trim(),
            RecipeId = recipeId
        };

        _dbContext.RecipeIngredients.Add(ingredient);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RecipeIngredientDto
        {
            Id = ingredient.Id,
            Name = ingredient.Name,
            Quantity = ingredient.Quantity,
            Unit = ingredient.Unit,
            RecipeId = ingredient.RecipeId
        };
    }

    public async Task<RecipeIngredientDto?> UpdateIngredientAsync(Guid recipeId, Guid ingredientId, UpdateRecipeIngredientRequest request, CancellationToken cancellationToken = default)
    {
        ValidateIngredient(request.Name, request.Quantity, request.Unit);

        var ingredient = await _dbContext.RecipeIngredients
            .FirstOrDefaultAsync(x => x.Id == ingredientId && x.RecipeId == recipeId, cancellationToken);

        if (ingredient is null)
        {
            return null;
        }

        ingredient.Name = request.Name.Trim();
        ingredient.Quantity = request.Quantity;
        ingredient.Unit = request.Unit.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RecipeIngredientDto
        {
            Id = ingredient.Id,
            Name = ingredient.Name,
            Quantity = ingredient.Quantity,
            Unit = ingredient.Unit,
            RecipeId = ingredient.RecipeId
        };
    }

    public async Task<bool> DeleteIngredientAsync(Guid recipeId, Guid ingredientId, CancellationToken cancellationToken = default)
    {
        var ingredient = await _dbContext.RecipeIngredients
            .FirstOrDefaultAsync(x => x.Id == ingredientId && x.RecipeId == recipeId, cancellationToken);

        if (ingredient is null)
        {
            return false;
        }

        _dbContext.RecipeIngredients.Remove(ingredient);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static void ValidateRecipe(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Der Rezeptname ist erforderlich.");
        }
    }

    private static void ValidateIngredient(string name, decimal quantity, string unit)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Der Zutatenname ist erforderlich.");
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
