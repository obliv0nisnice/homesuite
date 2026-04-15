using HomeSuite.Application.DTOs.Catalog;
using HomeSuite.Application.Interfaces;
using HomeSuite.Domain.Entities;
using HomeSuite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HomeSuite.Infrastructure.Services;

public class CatalogService : ICatalogService
{
    private readonly HomeSuiteDbContext _dbContext;

    public CatalogService(HomeSuiteDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<CatalogItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.CatalogItems
            .OrderBy(x => x.Name)
            .Select(x => new CatalogItemDto
            {
                Id = x.Id,
                Name = x.Name,
                DefaultUnit = x.DefaultUnit,
                Category = x.Category,
                SearchTerm = x.SearchTerm,
                BrandHint = x.BrandHint,
                IsStaple = x.IsStaple
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<CatalogItemDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.CatalogItems
            .Where(x => x.Id == id)
            .Select(x => new CatalogItemDto
            {
                Id = x.Id,
                Name = x.Name,
                DefaultUnit = x.DefaultUnit,
                Category = x.Category,
                SearchTerm = x.SearchTerm,
                BrandHint = x.BrandHint,
                IsStaple = x.IsStaple
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CatalogItemDto> CreateAsync(CreateCatalogItemRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request.Name, request.DefaultUnit);

        var item = new CatalogItem
        {
            Name = request.Name.Trim(),
            DefaultUnit = request.DefaultUnit.Trim(),
            Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim(),
            SearchTerm = string.IsNullOrWhiteSpace(request.SearchTerm) ? null : request.SearchTerm.Trim(),
            BrandHint = string.IsNullOrWhiteSpace(request.BrandHint) ? null : request.BrandHint.Trim(),
            IsStaple = request.IsStaple
        };

        _dbContext.CatalogItems.Add(item);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CatalogItemDto
        {
            Id = item.Id,
            Name = item.Name,
            DefaultUnit = item.DefaultUnit,
            Category = item.Category,
            SearchTerm = item.SearchTerm,
            BrandHint = item.BrandHint,
            IsStaple = item.IsStaple
        };
    }

    public async Task<CatalogItemDto?> UpdateAsync(Guid id, UpdateCatalogItemRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request.Name, request.DefaultUnit);

        var item = await _dbContext.CatalogItems
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (item is null)
        {
            return null;
        }

        item.Name = request.Name.Trim();
        item.DefaultUnit = request.DefaultUnit.Trim();
        item.Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim();
        item.SearchTerm = string.IsNullOrWhiteSpace(request.SearchTerm) ? null : request.SearchTerm.Trim();
        item.BrandHint = string.IsNullOrWhiteSpace(request.BrandHint) ? null : request.BrandHint.Trim();
        item.IsStaple = request.IsStaple;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CatalogItemDto
        {
            Id = item.Id,
            Name = item.Name,
            DefaultUnit = item.DefaultUnit,
            Category = item.Category,
            SearchTerm = item.SearchTerm,
            BrandHint = item.BrandHint,
            IsStaple = item.IsStaple
        };
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.CatalogItems
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (item is null)
        {
            return false;
        }

        _dbContext.CatalogItems.Remove(item);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static void ValidateRequest(string name, string defaultUnit)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Der Name ist erforderlich.");
        }

        if (string.IsNullOrWhiteSpace(defaultUnit))
        {
            throw new ArgumentException("Die Standard-Einheit ist erforderlich.");
        }
    }
}
