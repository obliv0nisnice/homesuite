using HomeSuite.Application.DTOs.Transactions;

namespace HomeSuite.Application.Interfaces;

public interface ITransactionService
{
    Task<List<TransactionDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<TransactionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TransactionDto> CreateAsync(CreateTransactionRequest request, CancellationToken cancellationToken = default);
    Task<TransactionDto?> UpdateAsync(Guid id, UpdateTransactionRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
