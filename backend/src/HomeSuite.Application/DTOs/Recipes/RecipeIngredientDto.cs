namespace HomeSuite.Application.DTOs.Recipes;

public class RecipeIngredientDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public Guid RecipeId { get; set; }
}
