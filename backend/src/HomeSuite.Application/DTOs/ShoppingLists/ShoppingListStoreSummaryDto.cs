namespace HomeSuite.Application.DTOs.ShoppingLists;

public class ShoppingListStoreSummaryDto
{
    public string StoreName { get; set; } = string.Empty;

    public decimal TotalEstimatedPrice { get; set; }

    public int CoveredItemsCount { get; set; }
    public int TotalItemsCount { get; set; }

    public bool IsComplete { get; set; }
    public bool IsBestOption { get; set; }
}
