namespace HomeSuite.Application.DTOs.Recipes;

public class CreateRecipeRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int BaseServings { get; set; } = 1;
}
