namespace HomeSuite.Application.DTOs.Transactions;

public class UpdateTransactionRequest
{
    public string Title { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string? Note { get; set; }
    public Guid CategoryId { get; set; }

    // Recurring
    public bool IsRecurring { get; set; } = false;
    public string? RecurringInterval { get; set; }  // "weekly" | "monthly" | "quarterly" | "yearly"
}
