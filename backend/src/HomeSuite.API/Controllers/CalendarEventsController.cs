using HomeSuite.Application.DTOs.Calendar;
using HomeSuite.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HomeSuite.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CalendarEventsController : ControllerBase
{
    private readonly ICalendarEventService _calendarEventService;

    public CalendarEventsController(ICalendarEventService calendarEventService)
    {
        _calendarEventService = calendarEventService;
    }

    [HttpGet]
    public async Task<ActionResult<List<CalendarEventDto>>> GetByMonth(
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken cancellationToken)
    {
        var result = await _calendarEventService.GetByMonthAsync(year, month, cancellationToken);
        return Ok(result);
    }

    [HttpGet("day/{date}")]
    public async Task<ActionResult<List<CalendarEventDto>>> GetByDate(
        string date,
        CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParse(date, out var parsedDate))
        {
            return BadRequest(new { message = "Ungültiges Datum." });
        }

        var result = await _calendarEventService.GetByDateAsync(parsedDate, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<CalendarEventDto>> Create(
        [FromBody] CreateCalendarEventRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await _calendarEventService.CreateAsync(request, cancellationToken);
            return Ok(created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CalendarEventDto>> Update(
        Guid id,
        [FromBody] UpdateCalendarEventRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _calendarEventService.UpdateAsync(id, request, cancellationToken);

            if (updated is null)
            {
                return NotFound(new { message = "Termin nicht gefunden." });
            }

            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _calendarEventService.DeleteAsync(id, cancellationToken);

        if (!deleted)
        {
            return NotFound(new { message = "Termin nicht gefunden." });
        }

        return NoContent();
    }
}
