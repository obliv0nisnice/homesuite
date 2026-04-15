namespace HomeSuite.Application.DTOs.MealPlans;

public class MealPlanWeekIngredientDto
{
    public string Name { get; set; } = string.Empty;
    public decimal RequiredQuantity { get; set; }
    public decimal InventoryQuantity { get; set; }
    public decimal MissingQuantity { get; set; }
    public string Unit { get; set; } = string.Empty;
}
