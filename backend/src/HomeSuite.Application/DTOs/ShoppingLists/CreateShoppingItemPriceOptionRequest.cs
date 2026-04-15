namespace HomeSuite.Application.DTOs.ShoppingLists;

public class CreateShoppingItemPriceOptionRequest
{
    public string StoreName { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }

    public string? ProductUrl { get; set; }
    public bool IsAvailable { get; set; } = true;
    public DateTime? CheckedAt { get; set; }
}
