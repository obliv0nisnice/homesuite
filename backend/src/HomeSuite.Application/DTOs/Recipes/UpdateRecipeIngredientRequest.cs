namespace HomeSuite.Application.DTOs.Recipes;

public class UpdateRecipeIngredientRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
}
