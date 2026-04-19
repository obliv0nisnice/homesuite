namespace HomeSuite.Application.DTOs.Calendar;

public class CalendarSubscriptionPreviewDto
{
    public string Url { get; set; } = string.Empty;
    public string? CalendarName { get; set; }
    public List<CalendarEventDto> Events { get; set; } = [];
}
