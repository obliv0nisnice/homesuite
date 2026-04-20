namespace HomeSuite.Application.DTOs.Calendar;

public class CalendarSubscriptionDto
{
    public Guid Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
