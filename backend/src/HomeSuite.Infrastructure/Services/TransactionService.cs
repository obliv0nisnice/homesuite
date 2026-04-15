using HomeSuite.Application.DTOs.Transactions;
using HomeSuite.Application.Interfaces;
using HomeSuite.Domain.Entities;
using HomeSuite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HomeSuite.Infrastructure.Services;

public class TransactionService : ITransactionService
{
    private readonly HomeSuiteDbContext _dbContext;

    public TransactionService(HomeSuiteDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<TransactionDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Transactions
            .Include(x => x.Category)
            .OrderByDescending(x => x.Date)
            .Select(x => new TransactionDto
            {
                Id = x.Id,
                Title = x.Title,
                Amount = x.Amount,
                Date = x.Date,
                Note = x.Note,
                CategoryId = x.CategoryId,
                CategoryName = x.Category != null ? x.Category.Name : string.Empty,
                CategoryType = x.Category != null ? x.Category.Type : string.Empty
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<TransactionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Transactions
            .Include(x => x.Category)
            .Where(x => x.Id == id)
            .Select(x => new TransactionDto
            {
                Id = x.Id,
                Title = x.Title,
                Amount = x.Amount,
                Date = x.Date,
                Note = x.Note,
                CategoryId = x.CategoryId,
                CategoryName = x.Category != null ? x.Category.Name : string.Empty,
                CategoryType = x.Category != null ? x.Category.Type : string.Empty
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<TransactionDto> CreateAsync(CreateTransactionRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request.Title, request.Amount, request.CategoryId);

        var category = await _dbContext.Categories
            .FirstOrDefaultAsync(x => x.Id == request.CategoryId, cancellationToken);

        if (category is null)
        {
            throw new InvalidOperationException("Die angegebene Kategorie existiert nicht.");
        }

        var transaction = new Transaction
        {
            Title = request.Title.Trim(),
            Amount = request.Amount,
            Date = request.Date,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CategoryId = request.CategoryId
        };

        _dbContext.Transactions.Add(transaction);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new TransactionDto
        {
            Id = transaction.Id,
            Title = transaction.Title,
            Amount = transaction.Amount,
            Date = transaction.Date,
            Note = transaction.Note,
            CategoryId = transaction.CategoryId,
            CategoryName = category.Name,
            CategoryType = category.Type
        };
    }

    public async Task<TransactionDto?> UpdateAsync(Guid id, UpdateTransactionRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request.Title, request.Amount, request.CategoryId);

        var transaction = await _dbContext.Transactions
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (transaction is null)
        {
            return null;
        }

        var category = await _dbContext.Categories
            .FirstOrDefaultAsync(x => x.Id == request.CategoryId, cancellationToken);

        if (category is null)
        {
            throw new InvalidOperationException("Die angegebene Kategorie existiert nicht.");
        }

        transaction.Title = request.Title.Trim();
        transaction.Amount = request.Amount;
        transaction.Date = request.Date;
        transaction.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        transaction.CategoryId = request.CategoryId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new TransactionDto
        {
            Id = transaction.Id,
            Title = transaction.Title,
            Amount = transaction.Amount,
            Date = transaction.Date,
            Note = transaction.Note,
            CategoryId = transaction.CategoryId,
            CategoryName = category.Name,
            CategoryType = category.Type
        };
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var transaction = await _dbContext.Transactions
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (transaction is null)
        {
            return false;
        }

        _dbContext.Transactions.Remove(transaction);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static void ValidateRequest(string title, decimal amount, Guid categoryId)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Der Titel ist erforderlich.");
        }

        if (amount <= 0)
        {
            throw new ArgumentException("Der Betrag muss größer als 0 sein.");
        }

        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException("Eine gültige Kategorie ist erforderlich.");
        }
    }
}
