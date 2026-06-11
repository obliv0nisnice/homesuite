using HomeSuite.Application.DTOs.Receipts;

namespace HomeSuite.Application.Interfaces;

public interface IReceiptService
{
    Task<ReceiptScanResultDto> ScanAsync(ScanReceiptRequest request, CancellationToken cancellationToken = default);
    Task ApplyAsync(ApplyReceiptRequest request, CancellationToken cancellationToken = default);
}
