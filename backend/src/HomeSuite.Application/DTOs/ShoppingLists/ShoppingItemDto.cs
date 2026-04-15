namespace HomeSuite.Application.DTOs.ShoppingLists;

public class ShoppingItemDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal RequiredQuantity { get; set; }
    public decimal InventoryQuantityUsed { get; set; }
    public decimal Quantity { get; set; }
    public decimal PurchasedQuantity { get; set; }

    public string Unit { get; set; } = string.Empty;
    public bool IsChecked { get; set; }

    public decimal? EstimatedUnitPrice { get; set; }
    public decimal? EstimatedTotalPrice { get; set; }
    public decimal? ActualTotalPrice { get; set; }

    public string SourceType { get; set; } = string.Empty;

    public Guid? CatalogItemId { get; set; }
    public string? CatalogItemName { get; set; }

    public List<ShoppingItemPriceOptionDto> PriceOptions { get; set; } = [];

    public Guid ShoppingListId { get; set; }
}
