namespace HomeSuite.Domain.Entities;

public class Recipe
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int BaseServings { get; set; } = 1;

    public List<RecipeIngredient> Ingredients { get; set; } = [];
}
