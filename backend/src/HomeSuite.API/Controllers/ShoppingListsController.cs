using HomeSuite.Application.DTOs.ShoppingLists;
using HomeSuite.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HomeSuite.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ShoppingListsController : ControllerBase
{
    private readonly IShoppingListService _shoppingListService;

    public ShoppingListsController(IShoppingListService shoppingListService)
    {
        _shoppingListService = shoppingListService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ShoppingListDto>>> GetAll(CancellationToken cancellationToken)
    {
        var shoppingLists = await _shoppingListService.GetAllAsync(cancellationToken);
        return Ok(shoppingLists);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ShoppingListDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var shoppingList = await _shoppingListService.GetByIdAsync(id, cancellationToken);

        if (shoppingList is null)
        {
            return NotFound(new { message = "Einkaufsliste nicht gefunden." });
        }

        return Ok(shoppingList);
    }

    [HttpPost]
    public async Task<ActionResult<ShoppingListDto>> Create(
        [FromBody] CreateShoppingListRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var createdShoppingList = await _shoppingListService.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = createdShoppingList.Id }, createdShoppingList);
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
    public async Task<ActionResult<ShoppingListDto>> Update(
        Guid id,
        [FromBody] UpdateShoppingListRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var updatedShoppingList = await _shoppingListService.UpdateAsync(id, request, cancellationToken);

            if (updatedShoppingList is null)
            {
                return NotFound(new { message = "Einkaufsliste nicht gefunden." });
            }

            return Ok(updatedShoppingList);
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
        var deleted = await _shoppingListService.DeleteAsync(id, cancellationToken);

        if (!deleted)
        {
            return NotFound(new { message = "Einkaufsliste nicht gefunden." });
        }

        return NoContent();
    }

    [HttpPost("{shoppingListId:guid}/items")]
    public async Task<ActionResult<ShoppingItemDto>> AddItem(
        Guid shoppingListId,
        [FromBody] CreateShoppingItemRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var createdItem = await _shoppingListService.AddItemAsync(shoppingListId, request, cancellationToken);
            return Ok(createdItem);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPut("{shoppingListId:guid}/items/{itemId:guid}")]
    public async Task<ActionResult<ShoppingItemDto>> UpdateItem(
        Guid shoppingListId,
        Guid itemId,
        [FromBody] UpdateShoppingItemRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var updatedItem = await _shoppingListService.UpdateItemAsync(shoppingListId, itemId, request, cancellationToken);

            if (updatedItem is null)
            {
                return NotFound(new { message = "Einkaufsposten nicht gefunden." });
            }

            return Ok(updatedItem);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{shoppingListId:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> DeleteItem(Guid shoppingListId, Guid itemId, CancellationToken cancellationToken)
    {
        var deleted = await _shoppingListService.DeleteItemAsync(shoppingListId, itemId, cancellationToken);

        if (!deleted)
        {
            return NotFound(new { message = "Einkaufsposten nicht gefunden." });
        }

        return NoContent();
    }

    [HttpPost("{shoppingListId:guid}/items/{itemId:guid}/price-options")]
    public async Task<ActionResult<ShoppingItemPriceOptionDto>> AddPriceOption(
        Guid shoppingListId,
        Guid itemId,
        [FromBody] CreateShoppingItemPriceOptionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var createdOption = await _shoppingListService.AddPriceOptionAsync(shoppingListId, itemId, request, cancellationToken);
            return Ok(createdOption);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPut("{shoppingListId:guid}/items/{itemId:guid}/price-options/{priceOptionId:guid}")]
    public async Task<ActionResult<ShoppingItemPriceOptionDto>> UpdatePriceOption(
        Guid shoppingListId,
        Guid itemId,
        Guid priceOptionId,
        [FromBody] UpdateShoppingItemPriceOptionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var updatedOption = await _shoppingListService.UpdatePriceOptionAsync(shoppingListId, itemId, priceOptionId, request, cancellationToken);

            if (updatedOption is null)
            {
                return NotFound(new { message = "Preisoption nicht gefunden." });
            }

            return Ok(updatedOption);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{shoppingListId:guid}/items/{itemId:guid}/price-options/{priceOptionId:guid}")]
    public async Task<IActionResult> DeletePriceOption(
        Guid shoppingListId,
        Guid itemId,
        Guid priceOptionId,
        CancellationToken cancellationToken)
    {
        var deleted = await _shoppingListService.DeletePriceOptionAsync(shoppingListId, itemId, priceOptionId, cancellationToken);

        if (!deleted)
        {
            return NotFound(new { message = "Preisoption nicht gefunden." });
        }

        return NoContent();
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(
        Guid id,
        [FromBody] CompleteShoppingListRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var completed = await _shoppingListService.CompleteShoppingListAsync(id, request, cancellationToken);

            if (!completed)
            {
                return NotFound(new { message = "Einkaufsliste nicht gefunden." });
            }

            return NoContent();
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
}
