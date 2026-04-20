namespace HomeSuite.Domain.Entities;

public class Transaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string? Note { get; set; }

    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }

    // Recurring
    public bool IsRecurring { get; set; } = false;
    public string? RecurringInterval { get; set; }  // "weekly" | "monthly" | "quarterly" | "yearly"
    public DateTime? NextDueDate { get; set; }       // wann die nächste Instanz fällig ist
}
