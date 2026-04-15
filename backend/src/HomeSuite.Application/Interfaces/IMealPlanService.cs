using HomeSuite.Application.DTOs.MealPlans;

namespace HomeSuite.Application.Interfaces;

public interface IMealPlanService
{
    Task<List<MealPlanDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<MealPlanDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MealPlanDto> CreateAsync(CreateMealPlanRequest request, CancellationToken cancellationToken = default);
    Task<MealPlanDto?> UpdateAsync(Guid id, UpdateMealPlanRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
