using Memoressa.Application.Common;
using Memoressa.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Memoressa.Infrastructure.Services;

public class UploadSessionCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UploadSessionCleanupHostedService> _logger;
    private readonly UploadSessionCleanupOptions _options;

    public UploadSessionCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<UploadSessionCleanupOptions> options,
        ILogger<UploadSessionCleanupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromHours(Math.Max(1, _options.IntervalHours));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
                await RunCleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Upload session cleanup failed");
            }
        }
    }

    private async Task RunCleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var uploads = scope.ServiceProvider.GetRequiredService<IUploadService>();
        var count = await uploads.CleanupExpiredPendingUploadSessionsAsync(cancellationToken);
        if (count > 0)
        {
            _logger.LogInformation("Abandoned {Count} expired pending upload session(s)", count);
        }
    }
}
