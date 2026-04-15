using HomeSuite.Application.DTOs.ShoppingLists;

namespace HomeSuite.Application.Interfaces;

public interface IShoppingListService
{
    Task<List<ShoppingListDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ShoppingListDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ShoppingListDto> CreateAsync(CreateShoppingListRequest request, CancellationToken cancellationToken = default);
    Task<ShoppingListDto?> UpdateAsync(Guid id, UpdateShoppingListRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ShoppingItemDto> AddItemAsync(Guid shoppingListId, CreateShoppingItemRequest request, CancellationToken cancellationToken = default);
    Task<ShoppingItemDto?> UpdateItemAsync(Guid shoppingListId, Guid itemId, UpdateShoppingItemRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteItemAsync(Guid shoppingListId, Guid itemId, CancellationToken cancellationToken = default);

    Task<ShoppingListDto> AddRecipeToShoppingListAsync(Guid shoppingListId, Guid recipeId, CancellationToken cancellationToken = default);
    Task<ShoppingListDto> AddMealPlanWeekToShoppingListAsync(Guid shoppingListId, DateOnly weekStartDate, CancellationToken cancellationToken = default);
    Task CompleteShoppingListAsync(Guid shoppingListId, CancellationToken cancellationToken = default);

    Task<List<ShoppingItemPriceOptionDto>> GetPriceOptionsAsync(Guid shoppingListId, Guid shoppingItemId, CancellationToken cancellationToken = default);
    Task<ShoppingItemPriceOptionDto> AddPriceOptionAsync(Guid shoppingListId, Guid shoppingItemId, CreateShoppingItemPriceOptionRequest request, CancellationToken cancellationToken = default);
    Task<ShoppingItemPriceOptionDto?> UpdatePriceOptionAsync(Guid shoppingListId, Guid shoppingItemId, Guid priceOptionId, UpdateShoppingItemPriceOptionRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeletePriceOptionAsync(Guid shoppingListId, Guid shoppingItemId, Guid priceOptionId, CancellationToken cancellationToken = default);
Task<List<ShoppingListStoreSummaryDto>> GetStoreSummariesAsync(Guid shoppingListId, CancellationToken cancellationToken = default);
}
