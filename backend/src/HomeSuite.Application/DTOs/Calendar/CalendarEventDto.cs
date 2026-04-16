namespace HomeSuite.Application.DTOs.Calendar;

public class CalendarEventDto
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public string Title { get; set; } = string.Empty;
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string? Notes { get; set; }
}
