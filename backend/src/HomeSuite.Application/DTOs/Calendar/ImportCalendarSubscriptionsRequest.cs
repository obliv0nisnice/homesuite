namespace HomeSuite.Application.DTOs.Calendar;

public class ImportCalendarSubscriptionsRequest
{
    public int Year { get; set; }
    public int Month { get; set; }
    public List<string> Urls { get; set; } = [];
}
