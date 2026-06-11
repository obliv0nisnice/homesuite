using HomeSuite.Application.Interfaces;
using HomeSuite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HomeSuite.API.BackgroundServices;

public class CatalogPriceRefreshWorker : BackgroundService
{
    private static readonly TimeSpan DailyRunTime = TimeSpan.FromHours(3);
    private static readonly TimeSpan StartupRefreshThreshold = TimeSpan.FromHours(20);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CatalogPriceRefreshWorker> _logger;

    public CatalogPriceRefreshWorker(
        IServiceProvider serviceProvider,
        ILogger<CatalogPriceRefreshWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        // Beim Start nur crawlen, wenn die Preise wirklich alt sind — sonst löst
        // jeder Deploy/Neustart einen kompletten Crawl aus.
        if (await ShouldRefreshOnStartupAsync(stoppingToken))
        {
            _logger.LogInformation("Preise älter als {Threshold} — starte initialen Katalog-Preisrefresh.", StartupRefreshThreshold);
            await RunRefreshAsync(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = DelayUntilNextRun(DateTime.Now);
            _logger.LogInformation("Nächster Katalog-Preisrefresh in {Delay}.", delay);
            await Task.Delay(delay, stoppingToken);

            await RunRefreshAsync(stoppingToken);
        }
    }

    private static TimeSpan DelayUntilNextRun(DateTime now)
    {
        var nextRun = now.Date.Add(DailyRunTime);
        if (nextRun <= now)
        {
            nextRun = nextRun.AddDays(1);
        }

        return nextRun - now;
    }

    private async Task<bool> ShouldRefreshOnStartupAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<HomeSuiteDbContext>();

            var hasCatalogItems = await dbContext.CatalogItems.AnyAsync(stoppingToken);
            if (!hasCatalogItems)
            {
                return false;
            }

            var latestCheckedAt = await dbContext.CatalogItemPrices
                .MaxAsync(x => (DateTime?)x.CheckedAt, stoppingToken);

            return latestCheckedAt is null
                || DateTime.UtcNow - latestCheckedAt.Value > StartupRefreshThreshold;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Konnte Preis-Alter nicht ermitteln, überspringe Startup-Refresh.");
            return false;
        }
    }

    private async Task RunRefreshAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var crawler = scope.ServiceProvider.GetRequiredService<ICatalogPriceCrawlerService>();

            await crawler.RefreshAllCatalogPricesAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim täglichen Katalog-Preisrefresh.");
        }
    }
}
