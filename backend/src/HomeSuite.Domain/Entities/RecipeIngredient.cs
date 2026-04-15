namespace HomeSuite.Domain.Entities;

public class RecipeIngredient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;

    public Guid RecipeId { get; set; }
    public Recipe? Recipe { get; set; }
}
