namespace HomeSuite.Application.DTOs.ShoppingLists;

public class CreateShoppingItemRequest
{
    public string Name { get; set; } = string.Empty;

    public decimal RequiredQuantity { get; set; }
    public decimal InventoryQuantityUsed { get; set; }
    public decimal Quantity { get; set; }
    public decimal PurchasedQuantity { get; set; }

    public string Unit { get; set; } = string.Empty;

    public decimal? EstimatedUnitPrice { get; set; }
    public decimal? EstimatedTotalPrice { get; set; }
    public decimal? ActualTotalPrice { get; set; }

    public string SourceType { get; set; } = "Manual";

    public Guid? CatalogItemId { get; set; }
}
