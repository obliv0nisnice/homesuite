namespace HomeSuite.Application.DTOs.ShoppingLists;

public class CreateShoppingItemRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
}
