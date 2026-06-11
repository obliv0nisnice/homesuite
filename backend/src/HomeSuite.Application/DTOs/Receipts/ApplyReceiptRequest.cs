using HomeSuite.Application.DTOs.ShoppingLists;

namespace HomeSuite.Application.DTOs.Receipts;

public class ApplyReceiptRequest
{
    public Guid ShoppingListId { get; set; }
    public string? StoreName { get; set; }
    public List<ReceiptLineDto> Lines { get; set; } = [];

    public bool CompleteList { get; set; }
    public CompleteShoppingListRequest? Complete { get; set; }
}
