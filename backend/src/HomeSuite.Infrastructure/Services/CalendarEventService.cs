using HomeSuite.Application.DTOs.Calendar;
using HomeSuite.Application.Interfaces;
using HomeSuite.Domain.Entities;
using HomeSuite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http;
using System.Globalization;
using System.Net.Http;

namespace HomeSuite.Infrastructure.Services;

public class CalendarEventService : ICalendarEventService
{
    private readonly HomeSuiteDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;

    public CalendarEventService(HomeSuiteDbContext dbContext, IHttpClientFactory httpClientFactory)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
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

    public async Task<List<CalendarSubscriptionPreviewDto>> ImportSubscriptionsAsync(
        ImportCalendarSubscriptionsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Year < 1 || request.Month is < 1 or > 12)
        {
            throw new ArgumentException("Ungültiger Monat für Kalender-Abo.");
        }

        var start = new DateOnly(request.Year, request.Month, 1);
        var end = start.AddMonths(1);
        var client = _httpClientFactory.CreateClient("CalendarSubscriptions");

        var urls = request.Urls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var results = new List<CalendarSubscriptionPreviewDto>();
        foreach (var url in urls)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException($"Ungültige Kalender-URL: {url}");
            }

            using var response = await client.GetAsync(uri, cancellationToken);
            response.EnsureSuccessStatusCode();

            var rawIcs = await response.Content.ReadAsStringAsync(cancellationToken);
            results.Add(ParseSubscription(url, rawIcs, start, end));
        }

        return results;
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
        Validate(request.Date, request.Title, request.StartTime, request.EndTime);

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
        Validate(request.Date, request.Title, request.StartTime, request.EndTime);

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

    private static void Validate(DateOnly date, string title, TimeOnly? startTime, TimeOnly? endTime)
    {
        if (date == default)
        {
            throw new ArgumentException("Ein gültiges Datum ist erforderlich.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Der Titel ist erforderlich.");
        }

        if (startTime.HasValue && endTime.HasValue && endTime.Value < startTime.Value)
        {
            throw new ArgumentException("Die Endzeit darf nicht vor der Startzeit liegen.");
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
            Notes = entity.Notes,
            IsImported = false
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
            Notes = entity.Notes,
            IsImported = false
        };
    }

    private static CalendarSubscriptionPreviewDto ParseSubscription(
        string url,
        string rawIcs,
        DateOnly rangeStart,
        DateOnly rangeEndExclusive)
    {
        var lines = UnfoldIcsLines(rawIcs);
        var calendarName = lines
            .FirstOrDefault(line => line.StartsWith("X-WR-CALNAME", StringComparison.OrdinalIgnoreCase))
            ?.Split(':', 2).ElementAtOrDefault(1)
            ?.Trim();

        var events = new List<CalendarEventDto>();
        Dictionary<string, string>? currentEvent = null;

        foreach (var line in lines)
        {
            if (line.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                currentEvent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                continue;
            }

            if (line.Equals("END:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                if (currentEvent is not null)
                {
                    var parsed = ParseEvent(currentEvent, url, calendarName, rangeStart, rangeEndExclusive);
                    if (parsed.Count > 0)
                    {
                        events.AddRange(parsed);
                    }
                }

                currentEvent = null;
                continue;
            }

            if (currentEvent is null)
            {
                continue;
            }

            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex];
            var value = line[(separatorIndex + 1)..];
            currentEvent[key] = value.Trim();
        }

        return new CalendarSubscriptionPreviewDto
        {
            Url = url,
            CalendarName = string.IsNullOrWhiteSpace(calendarName) ? url : UnescapeIcsText(calendarName),
            Events = events
                .OrderBy(x => x.Date)
                .ThenBy(x => x.StartTime)
                .ToList()
        };
    }

    private static List<string> UnfoldIcsLines(string rawIcs)
    {
        var normalized = rawIcs.Replace("\r\n", "\n").Replace('\r', '\n');
        var sourceLines = normalized.Split('\n', StringSplitOptions.None);
        var unfolded = new List<string>();

        foreach (var rawLine in sourceLines)
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            if ((line.StartsWith(' ') || line.StartsWith('\t')) && unfolded.Count > 0)
            {
                unfolded[^1] += line.TrimStart();
            }
            else
            {
                unfolded.Add(line);
            }
        }

        return unfolded;
    }

    private static List<CalendarEventDto> ParseEvent(
        Dictionary<string, string> data,
        string sourceUrl,
        string? calendarName,
        DateOnly rangeStart,
        DateOnly rangeEndExclusive)
    {
        var dtStartKey = data.Keys.FirstOrDefault(x => x.StartsWith("DTSTART", StringComparison.OrdinalIgnoreCase));
        if (dtStartKey is null || !TryParseIcsDateValue(data[dtStartKey], out var startDate, out var startTime))
        {
            return [];
        }

        var dtEndKey = data.Keys.FirstOrDefault(x => x.StartsWith("DTEND", StringComparison.OrdinalIgnoreCase));
        var hasEndDate = TryParseIcsDateValue(dtEndKey is null ? string.Empty : data[dtEndKey], out var endDate, out var endTime);

        var summary = data.TryGetValue("SUMMARY", out var title)
            ? UnescapeIcsText(title)
            : "Kalendertermin";
        var notes = data.TryGetValue("DESCRIPTION", out var description)
            ? UnescapeIcsText(description)
            : null;

        var effectiveEndDateExclusive = hasEndDate ? endDate : startDate.AddDays(1);
        if (effectiveEndDateExclusive <= startDate)
        {
            effectiveEndDateExclusive = startDate.AddDays(1);
        }

        var visibleStart = startDate > rangeStart ? startDate : rangeStart;
        var visibleEndExclusive = effectiveEndDateExclusive < rangeEndExclusive ? effectiveEndDateExclusive : rangeEndExclusive;

        if (visibleStart >= visibleEndExclusive)
        {
            return [];
        }

        var isAllDay = startTime is null && endTime is null;
        var events = new List<CalendarEventDto>();

        for (var date = visibleStart; date < visibleEndExclusive; date = date.AddDays(1))
        {
            var isFirstDay = date == startDate;
            var isLastDay = date == effectiveEndDateExclusive.AddDays(-1);

            events.Add(new CalendarEventDto
            {
                Id = Guid.NewGuid(),
                Date = date,
                Title = summary,
                StartTime = isAllDay ? null : (isFirstDay ? startTime : null),
                EndTime = isAllDay ? null : (isLastDay ? endTime : null),
                Notes = notes,
                IsImported = true,
                SourceName = string.IsNullOrWhiteSpace(calendarName) ? sourceUrl : UnescapeIcsText(calendarName),
                SourceUrl = sourceUrl
            });
        }

        return events;
    }

    private static bool TryParseIcsDateValue(string value, out DateOnly date, out TimeOnly? time)
    {
        date = default;
        time = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (DateOnly.TryParseExact(normalized, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
        {
            date = parsedDate;
            return true;
        }

        var trimmedZulu = normalized.EndsWith('Z') ? normalized[..^1] : normalized;
        if (DateTime.TryParseExact(trimmedZulu, "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedDateTime) ||
            DateTime.TryParseExact(trimmedZulu, "yyyyMMdd'T'HHmm", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out parsedDateTime))
        {
            date = DateOnly.FromDateTime(parsedDateTime);
            time = TimeOnly.FromDateTime(parsedDateTime);
            return true;
        }

        return false;
    }

    private static string UnescapeIcsText(string value) =>
        value
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\N", "\n", StringComparison.Ordinal)
            .Replace("\\,", ",", StringComparison.Ordinal)
            .Replace("\\;", ";", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal);
}
