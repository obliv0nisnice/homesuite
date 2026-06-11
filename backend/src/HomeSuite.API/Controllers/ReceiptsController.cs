using HomeSuite.Application.DTOs.Receipts;
using HomeSuite.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HomeSuite.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReceiptsController : ControllerBase
{
    private readonly IReceiptService _receiptService;
    private readonly ILogger<ReceiptsController> _logger;

    public ReceiptsController(IReceiptService receiptService, ILogger<ReceiptsController> logger)
    {
        _receiptService = receiptService;
        _logger = logger;
    }

    [HttpPost("scan")]
    public async Task<ActionResult<ReceiptScanResultDto>> Scan(
        [FromBody] ScanReceiptRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _receiptService.ScanAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Beleg-Scan fehlgeschlagen.");
            return StatusCode(502, new { message = "Beleg-Scan fehlgeschlagen. Details im Backend-Log." });
        }
    }

    [HttpPost("apply")]
    public async Task<IActionResult> Apply(
        [FromBody] ApplyReceiptRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _receiptService.ApplyAsync(request, cancellationToken);
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
