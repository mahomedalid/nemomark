using Knowledge.Core.Abstractions;
using Knowledge.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Knowledge.Worker;

/// <summary>
/// Watches the knowledge directory for Markdown changes and keeps the knowledge base in sync.
/// Performs a full ingestion pass on startup, then reacts to file system events. Only changed
/// documents are reprocessed (change detection is handled by the ingestion service via hashes).
/// </summary>
public sealed class IngestionWorker : BackgroundService
{
    private static readonly string[] MarkdownExtensions = { ".md", ".markdown" };

    private readonly IServiceProvider _services;
    private readonly ILogger<IngestionWorker> _logger;
    private readonly KnowledgeOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private FileSystemWatcher? _watcher;

    public IngestionWorker(
        IServiceProvider services,
        IOptions<KnowledgeOptions> options,
        ILogger<IngestionWorker> logger)
    {
        _services = services;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var directory = Path.GetFullPath(_options.KnowledgeDirectory);
        Directory.CreateDirectory(directory);
        _logger.LogInformation("Watching knowledge directory: {Directory}", directory);

        await RunInitialScanAsync(directory, stoppingToken);

        _watcher = new FileSystemWatcher(directory)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        _watcher.Created += (_, e) => Enqueue(() => IngestFileAsync(e.FullPath, stoppingToken));
        _watcher.Changed += (_, e) => Enqueue(() => IngestFileAsync(e.FullPath, stoppingToken));
        _watcher.Deleted += (_, e) => Enqueue(() => RemoveFileAsync(e.FullPath, stoppingToken));
        _watcher.Renamed += (_, e) =>
        {
            Enqueue(() => RemoveFileAsync(e.OldFullPath, stoppingToken));
            Enqueue(() => IngestFileAsync(e.FullPath, stoppingToken));
        };

        // Keep the service alive until cancellation.
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
    }

    private async Task RunInitialScanAsync(string directory, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _services.CreateAsyncScope();
            var ingestion = scope.ServiceProvider.GetRequiredService<IIngestionService>();
            await ingestion.IngestDirectoryAsync(directory, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initial ingestion scan failed.");
        }
    }

    private void Enqueue(Func<Task> work)
    {
        _ = Task.Run(async () =>
        {
            await _gate.WaitAsync();
            try
            {
                await work();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ingestion event handling failed.");
            }
            finally
            {
                _gate.Release();
            }
        });
    }

    private async Task IngestFileAsync(string path, CancellationToken cancellationToken)
    {
        if (!IsMarkdown(path) || !File.Exists(path))
        {
            return;
        }

        // Debounce editors that fire multiple write events.
        await Task.Delay(250, cancellationToken);

        var markdown = await ReadWithRetryAsync(path, cancellationToken);
        if (markdown is null)
        {
            return;
        }

        await using var scope = _services.CreateAsyncScope();
        var ingestion = scope.ServiceProvider.GetRequiredService<IIngestionService>();
        await ingestion.IngestFileAsync(path, markdown, cancellationToken);
    }

    private async Task RemoveFileAsync(string path, CancellationToken cancellationToken)
    {
        if (!IsMarkdown(path))
        {
            return;
        }

        await using var scope = _services.CreateAsyncScope();
        var ingestion = scope.ServiceProvider.GetRequiredService<IIngestionService>();
        await ingestion.RemoveFileAsync(path, cancellationToken);
    }

    private static async Task<string?> ReadWithRetryAsync(string path, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                return await File.ReadAllTextAsync(path, cancellationToken);
            }
            catch (IOException)
            {
                await Task.Delay(200, cancellationToken);
            }
        }

        return null;
    }

    private static bool IsMarkdown(string path) =>
        MarkdownExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public override void Dispose()
    {
        _watcher?.Dispose();
        _gate.Dispose();
        base.Dispose();
    }
}
