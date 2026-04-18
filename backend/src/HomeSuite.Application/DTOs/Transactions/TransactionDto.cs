namespace HomeSuite.Application.DTOs.Transactions;

public class TransactionDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string? Note { get; set; }

    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string CategoryType { get; set; } = string.Empty;

    // Recurring
    public bool IsRecurring { get; set; }
    public string? RecurringInterval { get; set; }
    public DateTime? NextDueDate { get; set; }
}
