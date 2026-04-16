namespace HomeSuite.Domain.Entities;

public class CalendarEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateOnly Date { get; set; }
    public string Title { get; set; } = string.Empty;
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string? Notes { get; set; }
}
