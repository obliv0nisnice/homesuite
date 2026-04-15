using HomeSuite.Application.DTOs.Recipes;
using HomeSuite.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HomeSuite.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecipesController : ControllerBase
{
    private readonly IRecipeService _recipeService;

    public RecipesController(IRecipeService recipeService)
    {
        _recipeService = recipeService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RecipeDto>>> GetAll(CancellationToken cancellationToken)
    {
        var recipes = await _recipeService.GetAllAsync(cancellationToken);
        return Ok(recipes);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RecipeDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var recipe = await _recipeService.GetByIdAsync(id, cancellationToken);

        if (recipe is null)
        {
            return NotFound(new { message = "Rezept nicht gefunden." });
        }

        return Ok(recipe);
    }

    [HttpPost]
    public async Task<ActionResult<RecipeDto>> Create(
        [FromBody] CreateRecipeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var createdRecipe = await _recipeService.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = createdRecipe.Id }, createdRecipe);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RecipeDto>> Update(
        Guid id,
        [FromBody] UpdateRecipeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var updatedRecipe = await _recipeService.UpdateAsync(id, request, cancellationToken);

            if (updatedRecipe is null)
            {
                return NotFound(new { message = "Rezept nicht gefunden." });
            }

            return Ok(updatedRecipe);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _recipeService.DeleteAsync(id, cancellationToken);

        if (!deleted)
        {
            return NotFound(new { message = "Rezept nicht gefunden." });
        }

        return NoContent();
    }

    [HttpPost("{recipeId:guid}/ingredients")]
    public async Task<ActionResult<RecipeIngredientDto>> AddIngredient(
        Guid recipeId,
        [FromBody] CreateRecipeIngredientRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var createdIngredient = await _recipeService.AddIngredientAsync(recipeId, request, cancellationToken);
            return Ok(createdIngredient);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{recipeId:guid}/ingredients/{ingredientId:guid}")]
    public async Task<ActionResult<RecipeIngredientDto>> UpdateIngredient(
        Guid recipeId,
        Guid ingredientId,
        [FromBody] UpdateRecipeIngredientRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var updatedIngredient = await _recipeService.UpdateIngredientAsync(recipeId, ingredientId, request, cancellationToken);

            if (updatedIngredient is null)
            {
                return NotFound(new { message = "Zutat nicht gefunden." });
            }

            return Ok(updatedIngredient);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{recipeId:guid}/ingredients/{ingredientId:guid}")]
    public async Task<IActionResult> DeleteIngredient(Guid recipeId, Guid ingredientId, CancellationToken cancellationToken)
    {
        var deleted = await _recipeService.DeleteIngredientAsync(recipeId, ingredientId, cancellationToken);

        if (!deleted)
        {
            return NotFound(new { message = "Zutat nicht gefunden." });
        }

        return NoContent();
    }
}
