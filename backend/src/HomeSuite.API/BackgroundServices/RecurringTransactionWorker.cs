using HomeSuite.Domain.Entities;
using HomeSuite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HomeSuite.API.BackgroundServices;

public class RecurringTransactionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecurringTransactionWorker> _logger;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    public RecurringTransactionWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<RecurringTransactionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RecurringTransactionWorker gestartet.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueRecurringTransactionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Verarbeiten wiederkehrender Transaktionen.");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task ProcessDueRecurringTransactionsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HomeSuiteDbContext>();

        var today = DateTime.UtcNow.Date;

        var due = await db.Transactions
            .Where(t => t.IsRecurring
                     && t.NextDueDate.HasValue
                     && t.NextDueDate.Value.Date <= today)
            .ToListAsync(ct);

        if (due.Count == 0)
        {
            _logger.LogInformation("Keine fälligen Recurring-Transaktionen am {Date}.", today.ToString("yyyy-MM-dd"));
            return;
        }

        _logger.LogInformation("{Count} Recurring-Transaktion(en) werden verarbeitet.", due.Count);

        foreach (var template in due)
        {
            var newTx = new Transaction
            {
                Id                = Guid.NewGuid(),
                Title             = template.Title,
                Amount            = template.Amount,
                Date              = today,
                Note              = template.Note,
                CategoryId        = template.CategoryId,
                IsRecurring       = false,
                RecurringInterval = null,
                NextDueDate       = null,
            };

            db.Transactions.Add(newTx);

            template.NextDueDate = CalculateNextDueDate(
                template.NextDueDate!.Value,
                template.RecurringInterval);
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Recurring-Transaktionen erfolgreich gespeichert.");
    }

    private static DateTime CalculateNextDueDate(DateTime current, string? interval) =>
        interval switch
        {
            "weekly" => current.AddDays(7),
            "yearly" => current.AddYears(1),
            "quarterly" => current.AddMonths(3),
            _        => current.AddMonths(1),
        };
}
