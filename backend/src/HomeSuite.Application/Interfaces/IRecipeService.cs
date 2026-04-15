using HomeSuite.Application.DTOs.Recipes;

namespace HomeSuite.Application.Interfaces;

public interface IRecipeService
{
    Task<List<RecipeDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<RecipeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RecipeDto> CreateAsync(CreateRecipeRequest request, CancellationToken cancellationToken = default);
    Task<RecipeDto?> UpdateAsync(Guid id, UpdateRecipeRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<RecipeIngredientDto> AddIngredientAsync(Guid recipeId, CreateRecipeIngredientRequest request, CancellationToken cancellationToken = default);
    Task<RecipeIngredientDto?> UpdateIngredientAsync(Guid recipeId, Guid ingredientId, UpdateRecipeIngredientRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteIngredientAsync(Guid recipeId, Guid ingredientId, CancellationToken cancellationToken = default);
}
