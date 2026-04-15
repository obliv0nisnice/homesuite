using HomeSuite.Application.DTOs.Inventory;

namespace HomeSuite.Application.Interfaces;

public interface IInventoryService
{
    Task<List<InventoryItemDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<InventoryItemDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<InventoryItemDto> CreateAsync(CreateInventoryItemRequest request, CancellationToken cancellationToken = default);
    Task<InventoryItemDto?> UpdateAsync(Guid id, UpdateInventoryItemRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
