using HomeSuite.Application.DTOs.Calendar;

namespace HomeSuite.Application.Interfaces;

public interface ICalendarEventService
{
    Task<List<CalendarEventDto>> GetByMonthAsync(int year, int month, CancellationToken cancellationToken = default);
    Task<List<CalendarEventDto>> GetByDateAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<List<CalendarSubscriptionPreviewDto>> ImportSubscriptionsAsync(ImportCalendarSubscriptionsRequest request, CancellationToken cancellationToken = default);
    Task<CalendarEventDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CalendarEventDto> CreateAsync(CreateCalendarEventRequest request, CancellationToken cancellationToken = default);
    Task<CalendarEventDto?> UpdateAsync(Guid id, UpdateCalendarEventRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
