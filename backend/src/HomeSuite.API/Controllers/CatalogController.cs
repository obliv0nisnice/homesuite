using HomeSuite.Application.DTOs.Catalog;
using HomeSuite.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HomeSuite.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CatalogController : ControllerBase
{
    private readonly ICatalogService _catalogService;

    public CatalogController(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }


[HttpPost("{id:guid}/refresh-prices")]
public async Task<ActionResult> RefreshPrices(Guid id, CancellationToken cancellationToken)
{
    try
    {
        await _catalogService.RefreshPricesAsync(id, cancellationToken);
        return NoContent();
    }
    catch (InvalidOperationException ex)
    {
        return NotFound(new { message = ex.Message });
    }
}

[HttpPost("refresh-prices")]
public async Task<ActionResult> RefreshAllPrices(CancellationToken cancellationToken)
{
    await _catalogService.RefreshAllPricesAsync(cancellationToken);
    return NoContent();
}
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CatalogItemDto>>> GetAll(CancellationToken cancellationToken)
    {
        var items = await _catalogService.GetAllAsync(cancellationToken);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CatalogItemDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _catalogService.GetByIdAsync(id, cancellationToken);

        if (item is null)
        {
            return NotFound(new { message = "CatalogItem nicht gefunden." });
        }

        return Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<CatalogItemDto>> Create(
        [FromBody] CreateCatalogItemRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var createdItem = await _catalogService.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, createdItem);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CatalogItemDto>> Update(
        Guid id,
        [FromBody] UpdateCatalogItemRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var updatedItem = await _catalogService.UpdateAsync(id, request, cancellationToken);

            if (updatedItem is null)
            {
                return NotFound(new { message = "CatalogItem nicht gefunden." });
            }

            return Ok(updatedItem);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _catalogService.DeleteAsync(id, cancellationToken);

        if (!deleted)
        {
            return NotFound(new { message = "CatalogItem nicht gefunden." });
        }

        return NoContent();
    }
}
