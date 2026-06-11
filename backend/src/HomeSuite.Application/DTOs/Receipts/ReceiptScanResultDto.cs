namespace HomeSuite.Application.DTOs.Receipts;

public class ReceiptScanResultDto
{
    public string? StoreName { get; set; }
    public string? PurchaseDate { get; set; }
    public decimal? TotalAmount { get; set; }
    public List<ReceiptLineDto> Lines { get; set; } = [];
}

public class ReceiptLineDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal? UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public Guid? ShoppingItemId { get; set; }
    public Guid? CatalogItemId { get; set; }
}
