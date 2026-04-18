using HomeSuite.Application.DTOs.ShoppingLists;

namespace HomeSuite.Application.Interfaces;

public interface IShoppingListService
{
    Task<List<ShoppingListDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ShoppingListDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ShoppingListDto> CreateAsync(CreateShoppingListRequest request, CancellationToken cancellationToken = default);
    Task<ShoppingListDto?> UpdateAsync(Guid id, UpdateShoppingListRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CompleteShoppingListAsync(
        Guid id,
        CompleteShoppingListRequest? request = null,
        CancellationToken cancellationToken = default);
}
