namespace HomeSuite.Application.DTOs.MealPlans;

public class CreateMealPlanRequest
{
    public DateOnly Date { get; set; }
    public string MealType { get; set; } = string.Empty;
    public int Servings { get; set; } = 1;
    public string? Notes { get; set; }
    public Guid RecipeId { get; set; }
}
