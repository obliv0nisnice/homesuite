using HomeSuite.Application.DTOs.Calendar;
using HomeSuite.Application.Interfaces;
using HomeSuite.Domain.Entities;
using HomeSuite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HomeSuite.Infrastructure.Services;

public class CalendarEventService : ICalendarEventService
{
    private readonly HomeSuiteDbContext _dbContext;

    public CalendarEventService(HomeSuiteDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<CalendarEventDto>> GetByMonthAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1);

        return await _dbContext.CalendarEvents
            .Where(x => x.Date >= start && x.Date < end)
            .OrderBy(x => x.Date)
            .ThenBy(x => x.StartTime)
            .Select(MapExpression())
            .ToListAsync(cancellationToken);
    }

    public async Task<List<CalendarEventDto>> GetByDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        return await _dbContext.CalendarEvents
            .Where(x => x.Date == date)
            .OrderBy(x => x.StartTime)
            .Select(MapExpression())
            .ToListAsync(cancellationToken);
    }

    public async Task<CalendarEventDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.CalendarEvents
            .Where(x => x.Id == id)
            .Select(MapExpression())
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CalendarEventDto> CreateAsync(CreateCalendarEventRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request.Title);

        var entity = new CalendarEvent
        {
            Date = request.Date,
            Title = request.Title.Trim(),
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        };

        _dbContext.CalendarEvents.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(entity);
    }

    public async Task<CalendarEventDto?> UpdateAsync(Guid id, UpdateCalendarEventRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request.Title);

        var entity = await _dbContext.CalendarEvents
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        entity.Date = request.Date;
        entity.Title = request.Title.Trim();
        entity.StartTime = request.StartTime;
        entity.EndTime = request.EndTime;
        entity.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(entity);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.CalendarEvents
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return false;
        }

        _dbContext.CalendarEvents.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static void Validate(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Der Titel ist erforderlich.");
        }
    }

    private static CalendarEventDto Map(CalendarEvent entity)
    {
        return new CalendarEventDto
        {
            Id = entity.Id,
            Date = entity.Date,
            Title = entity.Title,
            StartTime = entity.StartTime,
            EndTime = entity.EndTime,
            Notes = entity.Notes
        };
    }

    private static System.Linq.Expressions.Expression<Func<CalendarEvent, CalendarEventDto>> MapExpression()
    {
        return entity => new CalendarEventDto
        {
            Id = entity.Id,
            Date = entity.Date,
            Title = entity.Title,
            StartTime = entity.StartTime,
            EndTime = entity.EndTime,
            Notes = entity.Notes
        };
    }
}
