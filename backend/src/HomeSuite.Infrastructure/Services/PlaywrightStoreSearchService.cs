using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace HomeSuite.Infrastructure.Services;

public sealed class CatalogCrawlerOptions
{
    public string? SourceRoot { get; set; }
    public string NixExecutablePath { get; set; } = "nix";
}

public sealed class PlaywrightStoreSearchRequest
{
    public string SearchUrl { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int ResultLimit { get; set; } = 40;
    public List<string> PreferredUrlFragments { get; set; } = [];
}

public sealed class PlaywrightStoreSearchResult
{
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool ChallengeDetected { get; set; }
    public List<string> Links { get; set; } = [];
}

public interface IPlaywrightStoreSearchService
{
    Task<PlaywrightStoreSearchResult?> SearchAsync(
        PlaywrightStoreSearchRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class PlaywrightStoreSearchService : IPlaywrightStoreSearchService
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(60);

    private readonly CatalogCrawlerOptions _options;
    private readonly ILogger<PlaywrightStoreSearchService> _logger;

    public PlaywrightStoreSearchService(
        CatalogCrawlerOptions options,
        ILogger<PlaywrightStoreSearchService> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<PlaywrightStoreSearchResult?> SearchAsync(
        PlaywrightStoreSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.SearchUrl) || string.IsNullOrWhiteSpace(request.Host))
        {
            return null;
        }

        var sourceRoot = ResolveSourceRoot();
        if (sourceRoot is null)
        {
            _logger.LogDebug("Playwright-Suche übersprungen: kein HomeSuite-SourceRoot gefunden.");
            return null;
        }

        var scriptPath = Path.Combine(sourceRoot, "scripts", "search-store-playwright.mjs");
        if (!File.Exists(scriptPath))
        {
            _logger.LogDebug("Playwright-Suche übersprungen: Script nicht gefunden unter {ScriptPath}", scriptPath);
            return null;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _options.NixExecutablePath,
            WorkingDirectory = sourceRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("develop");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("node");
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("--url");
        startInfo.ArgumentList.Add(request.SearchUrl);
        startInfo.ArgumentList.Add("--host");
        startInfo.ArgumentList.Add(request.Host);
        startInfo.ArgumentList.Add("--limit");
        startInfo.ArgumentList.Add(request.ResultLimit.ToString());

        foreach (var fragment in request.PreferredUrlFragments.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            startInfo.ArgumentList.Add("--match");
            startInfo.ArgumentList.Add(fragment);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        // Ohne Timeout blockiert ein hängendes Chromium den kompletten Preis-Refresh.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(ProcessTimeout);

        var stdoutTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var stderrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            TryKillProcess(process);

            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            _logger.LogWarning(
                "Playwright-Suche nach {Timeout} abgebrochen für {Url}.",
                ProcessTimeout,
                request.SearchUrl);
            return null;
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            _logger.LogWarning(
                "Playwright-Suche fehlgeschlagen für {Url}. ExitCode={ExitCode}. STDERR: {StdErr}",
                request.SearchUrl,
                process.ExitCode,
                string.IsNullOrWhiteSpace(stderr) ? "<leer>" : stderr.Trim());
            return null;
        }

        var json = ExtractJson(stdout);
        if (string.IsNullOrWhiteSpace(json))
        {
            _logger.LogWarning("Playwright-Suche lieferte kein parsebares JSON für {Url}", request.SearchUrl);
            return null;
        }

        try
        {
            var result = JsonSerializer.Deserialize<PlaywrightStoreSearchResult>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Playwright-JSON konnte nicht geparst werden für {Url}: {Json}", request.SearchUrl, json);
            return null;
        }
    }

    private void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Playwright-Prozess konnte nicht beendet werden.");
        }
    }

    private string? ResolveSourceRoot()
    {
        var candidates = new[]
        {
            _options.SourceRoot,
            Environment.GetEnvironmentVariable("HOMESUITE_SOURCE_ROOT"),
            Directory.GetCurrentDirectory()
        };

        return candidates
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .FirstOrDefault(IsValidSourceRoot);
    }

    private static bool IsValidSourceRoot(string path)
    {
        return Directory.Exists(path)
            && File.Exists(Path.Combine(path, "flake.nix"))
            && File.Exists(Path.Combine(path, "scripts", "search-store-playwright.mjs"));
    }

    private static string? ExtractJson(string stdout)
    {
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return null;
        }

        var lines = stdout
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var firstJsonLine = lines.FindIndex(line => line.StartsWith("{", StringComparison.Ordinal));
        if (firstJsonLine < 0)
        {
            return null;
        }

        return string.Join(Environment.NewLine, lines.Skip(firstJsonLine));
    }
}
