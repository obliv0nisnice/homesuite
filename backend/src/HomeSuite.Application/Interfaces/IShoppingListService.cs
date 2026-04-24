using HomeSuite.Application.DTOs.ShoppingLists;

namespace HomeSuite.Application.Interfaces;

public interface IShoppingListService
{
    Task<List<ShoppingListDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ShoppingListDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ShoppingListDto> CreateAsync(CreateShoppingListRequest request, CancellationToken cancellationToken = default);
    Task<ShoppingListDto?> UpdateAsync(Guid id, UpdateShoppingListRequest request, CancellationToken cancellationToken = default);
    Task<ShoppingItemDto> AddItemAsync(Guid shoppingListId, CreateShoppingItemRequest request, CancellationToken cancellationToken = default);
    Task<ShoppingItemDto?> UpdateItemAsync(Guid shoppingListId, Guid itemId, UpdateShoppingItemRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteItemAsync(Guid shoppingListId, Guid itemId, CancellationToken cancellationToken = default);
    Task<ShoppingItemPriceOptionDto> AddPriceOptionAsync(Guid shoppingListId, Guid itemId, CreateShoppingItemPriceOptionRequest request, CancellationToken cancellationToken = default);
    Task<ShoppingItemPriceOptionDto?> UpdatePriceOptionAsync(Guid shoppingListId, Guid itemId, Guid priceOptionId, UpdateShoppingItemPriceOptionRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeletePriceOptionAsync(Guid shoppingListId, Guid itemId, Guid priceOptionId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CompleteShoppingListAsync(
        Guid id,
        CompleteShoppingListRequest? request = null,
        CancellationToken cancellationToken = default);
}
