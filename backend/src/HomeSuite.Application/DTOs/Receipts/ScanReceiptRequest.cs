namespace HomeSuite.Application.DTOs.Receipts;

public class ScanReceiptRequest
{
    public string ImageBase64 { get; set; } = string.Empty;
    public string MediaType { get; set; } = "image/jpeg";
    public Guid? ShoppingListId { get; set; }
}
