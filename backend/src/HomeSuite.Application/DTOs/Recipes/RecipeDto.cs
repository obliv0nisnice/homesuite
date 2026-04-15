namespace HomeSuite.Application.DTOs.Recipes;

public class RecipeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<RecipeIngredientDto> Ingredients { get; set; } = [];
}
