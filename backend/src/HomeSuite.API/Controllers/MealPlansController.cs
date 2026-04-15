using HomeSuite.Application.DTOs.MealPlans;
using HomeSuite.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HomeSuite.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MealPlansController : ControllerBase
{
    [HttpGet("week-summary")]
    public async Task<ActionResult<MealPlanWeekSummaryDto>> GetWeekSummary(
        [FromQuery] DateOnly weekStartDate,
        CancellationToken cancellationToken)
    {
        var summary = await _mealPlanService.GetWeekSummaryAsync(weekStartDate, cancellationToken);
        return Ok(summary);
    }


    private readonly IMealPlanService _mealPlanService;

    public MealPlansController(IMealPlanService mealPlanService)
    {
        _mealPlanService = mealPlanService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<MealPlanDto>>> GetAll(CancellationToken cancellationToken)
    {
        var mealPlans = await _mealPlanService.GetAllAsync(cancellationToken);
        return Ok(mealPlans);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MealPlanDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var mealPlan = await _mealPlanService.GetByIdAsync(id, cancellationToken);

        if (mealPlan is null)
        {
            return NotFound(new { message = "Meal Plan nicht gefunden." });
        }

        return Ok(mealPlan);
    }

    [HttpPost]
    public async Task<ActionResult<MealPlanDto>> Create(
        [FromBody] CreateMealPlanRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var createdMealPlan = await _mealPlanService.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = createdMealPlan.Id }, createdMealPlan);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MealPlanDto>> Update(
        Guid id,
        [FromBody] UpdateMealPlanRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var updatedMealPlan = await _mealPlanService.UpdateAsync(id, request, cancellationToken);

            if (updatedMealPlan is null)
            {
                return NotFound(new { message = "Meal Plan nicht gefunden." });
            }

            return Ok(updatedMealPlan);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _mealPlanService.DeleteAsync(id, cancellationToken);

        if (!deleted)
        {
            return NotFound(new { message = "Meal Plan nicht gefunden." });
        }

        return NoContent();
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _mealPlanService.CompleteAsync(id, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
