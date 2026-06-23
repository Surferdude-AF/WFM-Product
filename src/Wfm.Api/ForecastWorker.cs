using Wfm.Forecasting.Infrastructure;

namespace Wfm.Api;

// Hosts the forecast job loop in the API process (step 11b): poll the queue, run
// the next job, repeat. Connects as the platform `wfm_worker` role. Disabled when
// no worker connection is configured, so it never interferes where it isn't wanted
// (e.g. acceptance tests that don't exercise the worker).
public sealed partial class ForecastWorker(IConfiguration configuration, ILogger<ForecastWorker> logger) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ErrorBackoff = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = configuration.GetConnectionString("WfmWorker");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            LogDisabled(logger);
            return;
        }

        var processor = new ForecastJobProcessor(connectionString);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await processor.ProcessNextAsync(stoppingToken))
                {
                    await Task.Delay(IdleDelay, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                LogJobFailed(logger, ex);
                await Task.Delay(ErrorBackoff, stoppingToken);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Forecast worker disabled: no 'WfmWorker' connection string configured.")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Forecast job processing failed.")]
    private static partial void LogJobFailed(ILogger logger, Exception exception);
}
