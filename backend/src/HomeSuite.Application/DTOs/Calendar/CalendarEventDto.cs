namespace HomeSuite.Application.DTOs.Calendar;

public class CalendarEventDto
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public string Title { get; set; } = string.Empty;
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string? Notes { get; set; }
    public bool IsImported { get; set; }
    public string? SourceName { get; set; }
    public string? SourceUrl { get; set; }
}
