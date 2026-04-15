namespace HomeSuite.Application.DTOs.MealPlans;

public class MealPlanDto
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public string MealType { get; set; } = string.Empty;
    public int Servings { get; set; }
    public string? Notes { get; set; }
    public bool IsCompleted { get; set; }

    public Guid RecipeId { get; set; }
    public string RecipeName { get; set; } = string.Empty;
}
