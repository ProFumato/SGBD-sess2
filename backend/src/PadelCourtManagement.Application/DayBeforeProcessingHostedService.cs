using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PadelCourtManagement.Application;

public sealed class DayBeforeProcessingOptions
{
    public TimeSpan Interval { get; set; } = TimeSpan.FromDays(1);
    public bool RunOnStartup { get; set; } = true;
}

public sealed class DayBeforeProcessingRunner(
    IDayBeforeService service,
    TimeProvider clock,
    ILogger<DayBeforeProcessingRunner> logger)
{
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await service.ProcessAsync(clock.GetUtcNow(), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Day-before processing failed.");
            throw;
        }
    }
}

public sealed class DayBeforeProcessingHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<DayBeforeProcessingOptions> options,
    ILogger<DayBeforeProcessingHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (settings.Interval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Day-before processing interval must be greater than zero.");
        }

        if (settings.RunOnStartup)
        {
            await RunOnceAsync(stoppingToken);
        }

        using var timer = new PeriodicTimer(settings.Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var runner = scope.ServiceProvider.GetRequiredService<DayBeforeProcessingRunner>();
        try
        {
            await runner.RunOnceAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            logger.LogError("The hosted day-before cycle completed with an error; the next cycle will retry.");
        }
    }
}
