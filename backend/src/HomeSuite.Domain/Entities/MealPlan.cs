namespace HomeSuite.Domain.Entities;

public class MealPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateOnly Date { get; set; }
    public string MealType { get; set; } = string.Empty; // Breakfast / Lunch / Dinner / Snack
    public int Servings { get; set; } = 1;
    public string? Notes { get; set; }

    public Guid RecipeId { get; set; }
    public Recipe? Recipe { get; set; }
}
