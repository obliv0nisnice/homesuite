namespace HomeSuite.Application.DTOs.Recipes;

public class CreateRecipeIngredientRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
}
