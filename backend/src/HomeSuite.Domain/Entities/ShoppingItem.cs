namespace HomeSuite.Domain.Entities;

public class ShoppingItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

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

    public string SourceType { get; set; } = "Manual";

    public Guid? CatalogItemId { get; set; }
    public CatalogItem? CatalogItem { get; set; }

    public Guid ShoppingListId { get; set; }
    public ShoppingList? ShoppingList { get; set; }

    public List<ShoppingItemPriceOption> PriceOptions { get; set; } = [];
}
