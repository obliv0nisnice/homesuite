using HomeSuite.Application.DTOs.Catalog;

namespace HomeSuite.Application.Interfaces;

public interface ICatalogService
{
    Task<List<CatalogItemDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<CatalogItemDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CatalogItemDto> CreateAsync(CreateCatalogItemRequest request, CancellationToken cancellationToken = default);
    Task<CatalogItemDto?> UpdateAsync(Guid id, UpdateCatalogItemRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task RefreshPricesAsync(Guid id, CancellationToken cancellationToken = default);
    Task RefreshAllPricesAsync(CancellationToken cancellationToken = default);
}
