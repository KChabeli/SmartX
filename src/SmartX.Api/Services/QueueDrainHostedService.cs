namespace SmartX.Api.Services;

/// <summary>
/// Background drain: always pop PriorityQueue before Queue&lt;T&gt; without an HTTP caller asking
/// for the next envelope. Ingest only enqueues; this loop commits.
/// </summary>
public sealed class QueueDrainHostedService(
    RealtimeMeshEngine engine,
    TelemetryIngestService ingest,
    ILogger<QueueDrainHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Queue drain armed — priority lane is always emptied before FIFO.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var drained = 0;
            while (drained < 32 && engine.TryDequeueNext(out var envelope))
            {
                try
                {
                    ingest.CommitEnvelope(envelope);
                    drained++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Drain failed for {Mac}", envelope.DeviceId);
                    break;
                }
            }

            if (drained > 0)
            {
                engine.RefreshDisconnects(DateTimeOffset.UtcNow);
                await engine.BroadcastQueuesAsync();
            }

            await Task.Delay(TimeSpan.FromMilliseconds(80));
        }
    }
}
