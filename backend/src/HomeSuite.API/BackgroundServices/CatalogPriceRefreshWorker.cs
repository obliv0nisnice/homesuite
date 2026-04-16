using HomeSuite.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HomeSuite.API.BackgroundServices;

public class CatalogPriceRefreshWorker : BackgroundService
{
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

        while (!stoppingToken.IsCancellationRequested)
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

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
