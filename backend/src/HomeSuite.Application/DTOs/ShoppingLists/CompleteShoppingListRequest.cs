namespace HomeSuite.Application.DTOs.ShoppingLists;

public class CompleteShoppingListRequest
{
    public bool CreateBudgetExpense { get; set; }
    public Guid? ExpenseCategoryId { get; set; }
    public string? TransactionTitle { get; set; }
    public DateTime? TransactionDate { get; set; }
}
