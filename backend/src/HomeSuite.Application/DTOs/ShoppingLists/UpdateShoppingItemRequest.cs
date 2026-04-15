namespace HomeSuite.Application.DTOs.ShoppingLists;

public class UpdateShoppingItemRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public bool IsChecked { get; set; }
}
