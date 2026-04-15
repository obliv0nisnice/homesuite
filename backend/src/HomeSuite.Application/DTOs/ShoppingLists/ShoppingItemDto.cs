namespace HomeSuite.Application.DTOs.ShoppingLists;

public class ShoppingItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public bool IsChecked { get; set; }
    public Guid ShoppingListId { get; set; }
}
