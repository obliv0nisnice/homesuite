namespace HomeSuite.Domain.Entities;

public class ShoppingItemPriceOption
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ShoppingItemId { get; set; }
    public ShoppingItem? ShoppingItem { get; set; }

    public string StoreName { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }

    public string? ProductUrl { get; set; }
    public bool IsAvailable { get; set; } = true;
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}
