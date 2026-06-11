using HomeSuite.Application.DTOs.Categories;
using HomeSuite.Application.Interfaces;
using HomeSuite.Domain.Entities;
using HomeSuite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HomeSuite.Infrastructure.Services;

public class CategoryService : ICategoryService
{
    private readonly HomeSuiteDbContext _dbContext;

    public CategoryService(HomeSuiteDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<CategoryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Categories
            .OrderBy(x => x.Name)
            .Select(x => new CategoryDto
            {
                Id = x.Id,
                Name = x.Name,
                Type = x.Type,
                MonthlyLimit = x.MonthlyLimit
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<CategoryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Categories
            .Where(x => x.Id == id)
            .Select(x => new CategoryDto
            {
                Id = x.Id,
                Name = x.Name,
                Type = x.Type,
                MonthlyLimit = x.MonthlyLimit
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedType = ValidateAndNormalize(request.Name, request.Type);

        var category = new Category
        {
            Name = request.Name.Trim(),
            Type = normalizedType,
            MonthlyLimit = NormalizeMonthlyLimit(request.MonthlyLimit, normalizedType)
        };

        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Type = category.Type,
            MonthlyLimit = category.MonthlyLimit
        };
    }

    public async Task<CategoryDto?> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.Categories
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (category is null)
        {
            return null;
        }

        var normalizedType = ValidateAndNormalize(request.Name, request.Type);

        category.Name = request.Name.Trim();
        category.Type = normalizedType;
        category.MonthlyLimit = NormalizeMonthlyLimit(request.MonthlyLimit, normalizedType);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Type = category.Type,
            MonthlyLimit = category.MonthlyLimit
        };
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.Categories
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (category is null)
        {
            return false;
        }

        var isUsedByTransactions = await _dbContext.Transactions
            .AnyAsync(x => x.CategoryId == id, cancellationToken);

        if (isUsedByTransactions)
        {
            throw new InvalidOperationException("Die Kategorie wird noch von Transaktionen verwendet und kann nicht gelöscht werden.");
        }

        _dbContext.Categories.Remove(category);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static decimal? NormalizeMonthlyLimit(decimal? monthlyLimit, string type)
    {
        if (monthlyLimit is null || type != "Expense")
        {
            return null;
        }

        if (monthlyLimit < 0)
        {
            throw new ArgumentException("Das Monatslimit darf nicht negativ sein.");
        }

        return decimal.Round(monthlyLimit.Value, 2);
    }

    private static string ValidateAndNormalize(string name, string type)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Der Kategoriename ist erforderlich.");
        }

        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("Der Kategorietyp ist erforderlich.");
        }

        var normalizedType = type.Trim();

        if (normalizedType is not ("Income" or "Expense"))
        {
            throw new ArgumentException("Der Kategorietyp muss 'Income' oder 'Expense' sein.");
        }

        return normalizedType;
    }
}
