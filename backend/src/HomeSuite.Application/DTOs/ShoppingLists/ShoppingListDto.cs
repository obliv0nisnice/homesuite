namespace HomeSuite.Application.DTOs.ShoppingLists;

public class ShoppingListDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<ShoppingItemDto> Items { get; set; } = [];
}
