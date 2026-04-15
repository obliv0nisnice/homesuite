namespace HomeSuite.Application.DTOs.ShoppingLists;

public class ShoppingItemPriceOptionDto
{
    public Guid Id { get; set; }

    public Guid ShoppingItemId { get; set; }

    public string StoreName { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }

    public string? ProductUrl { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime CheckedAt { get; set; }
}
