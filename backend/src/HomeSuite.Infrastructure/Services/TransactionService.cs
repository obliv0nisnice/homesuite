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
                CategoryType = x.Category != null ? x.Category.Type : string.Empty,
                IsRecurring = x.IsRecurring,
                RecurringInterval = x.RecurringInterval,
                NextDueDate = x.NextDueDate
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
                CategoryType = x.Category != null ? x.Category.Type : string.Empty,
                IsRecurring = x.IsRecurring,
                RecurringInterval = x.RecurringInterval,
                NextDueDate = x.NextDueDate
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

        var normalizedAmount = NormalizeAmount(request.Amount, category.Type);
        var normalizedDate = NormalizeDate(request.Date);
        var recurringInterval = NormalizeRecurringInterval(request.IsRecurring, request.RecurringInterval);

        var transaction = new Transaction
        {
            Title = request.Title.Trim(),
            Amount = normalizedAmount,
            Date = normalizedDate,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CategoryId = request.CategoryId,
            IsRecurring = request.IsRecurring,
            RecurringInterval = recurringInterval,
            NextDueDate = CalculateFirstNextDueDate(normalizedDate, recurringInterval)
        };

        _dbContext.Transactions.Add(transaction);

        // Wiederkehrende Transaktionen sind reine Vorlagen und zählen nicht als
        // Buchung — die erste Monats-Buchung entsteht sofort als eigene Zeile,
        // damit sie einzeln (nur für diesen Monat) bearbeitbar ist.
        if (transaction.IsRecurring)
        {
            _dbContext.Transactions.Add(new Transaction
            {
                Title = transaction.Title,
                Amount = transaction.Amount,
                Date = transaction.Date,
                Note = transaction.Note,
                CategoryId = transaction.CategoryId,
                IsRecurring = false,
                RecurringInterval = null,
                NextDueDate = null
            });
        }

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
            CategoryType = category.Type,
            IsRecurring = transaction.IsRecurring,
            RecurringInterval = transaction.RecurringInterval,
            NextDueDate = transaction.NextDueDate
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

        var wasRecurring = transaction.IsRecurring;
        var previousInterval = transaction.RecurringInterval;
        var previousDate = transaction.Date;

        transaction.Title = request.Title.Trim();
        transaction.Amount = NormalizeAmount(request.Amount, category.Type);
        transaction.Date = NormalizeDate(request.Date);
        transaction.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        transaction.CategoryId = request.CategoryId;
        transaction.IsRecurring = request.IsRecurring;
        transaction.RecurringInterval = NormalizeRecurringInterval(request.IsRecurring, request.RecurringInterval);

        // Fälligkeit nur neu berechnen, wenn sich der Zeitplan tatsächlich ändert.
        // Ein pauschales Zurücksetzen würde bei jeder Bearbeitung alle seit dem
        // Startdatum vergangenen Intervalle erneut auslösen (Duplikat-Buchungen).
        if (!transaction.IsRecurring)
        {
            transaction.NextDueDate = null;
        }
        else if (!wasRecurring
            || transaction.RecurringInterval != previousInterval
            || transaction.Date != previousDate
            || transaction.NextDueDate is null)
        {
            transaction.NextDueDate = CalculateFirstNextDueDate(transaction.Date, transaction.RecurringInterval);
        }

        // Wird eine bestehende Buchung zur Vorlage, bleibt die Buchung selbst als
        // eigene Zeile erhalten (Vorlagen zählen nicht als Buchung).
        if (!wasRecurring && transaction.IsRecurring)
        {
            _dbContext.Transactions.Add(new Transaction
            {
                Title = transaction.Title,
                Amount = transaction.Amount,
                Date = transaction.Date,
                Note = transaction.Note,
                CategoryId = transaction.CategoryId,
                IsRecurring = false,
                RecurringInterval = null,
                NextDueDate = null
            });
        }

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
            CategoryType = category.Type,
            IsRecurring = transaction.IsRecurring,
            RecurringInterval = transaction.RecurringInterval,
            NextDueDate = transaction.NextDueDate
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

    private static decimal NormalizeAmount(decimal amount, string categoryType)
    {
        var absoluteAmount = Math.Abs(amount);

        return categoryType switch
        {
            "Income" => absoluteAmount,
            "Expense" => -absoluteAmount,
            _ => amount
        };
    }

    private static DateTime NormalizeDate(DateTime date)
    {
        return DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
    }

    private static string? NormalizeRecurringInterval(bool isRecurring, string? interval)
    {
        if (!isRecurring)
        {
            return null;
        }

        return interval?.Trim().ToLowerInvariant() switch
        {
            "weekly" => "weekly",
            "monthly" => "monthly",
            "quarterly" => "quarterly",
            "yearly" => "yearly",
            null or "" => "monthly",
            _ => throw new ArgumentException("Ungültiges Wiederholungsintervall.")
        };
    }

    private static DateTime? CalculateFirstNextDueDate(DateTime startDate, string? interval) =>
        interval switch
        {
            "weekly" => startDate.AddDays(7),
            "quarterly" => startDate.AddMonths(3),
            "yearly" => startDate.AddYears(1),
            "monthly" => startDate.AddMonths(1),
            _ => null
        };
}
