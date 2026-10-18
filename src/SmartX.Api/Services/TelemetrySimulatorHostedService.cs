using SmartX.Api.Contracts;
using SmartX.Domain.Telemetry;

namespace SmartX.Api.Services;

/// <summary>
/// Publishes mock ESP32 telemetry so the colour-coded dashboard can be evaluated under load.
/// Selected nodes are periodically silenced to demonstrate disconnect detection.
/// </summary>
public sealed class TelemetrySimulatorHostedService(
    GatewayStore store,
    TelemetryIngestService ingest,
    ILogger<TelemetrySimulatorHostedService> logger) : BackgroundService
{
    private int _tick;
    private readonly HashSet<string> _silenced = new(StringComparer.OrdinalIgnoreCase);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Smart-X mesh simulator armed with {Count} ESP32 nodes.", store.Sensors.Count);

        while (!stoppingToken.IsCancellationRequested)
        {
            _tick++;
            if (_tick % 18 == 0)
            {
                ToggleSilence();
            }

            foreach (var sensor in store.Sensors.Values)
            {
                if (_silenced.Contains(sensor.MacAddress) || stoppingToken.IsCancellationRequested)
                {
                    continue;
                }

                try
                {
                    Publish(sensor);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Simulator failed for {Mac}", sensor.MacAddress);
                }
            }

            // Do not pass stoppingToken into Delay — Visual Studio treats TaskCanceledException as a crash
            // when you press Stop Debugging. Poll the token instead.
            await Task.Delay(TimeSpan.FromMilliseconds(1600));
        }
    }

    private void Publish(Domain.Sensors.SensorProfile sensor)
    {
        var numeric = MeshSeeder.SampleNumeric(sensor, _tick + 400);
        ingest.Ingest(new TelemetryIngestRequest(
            sensor.MacAddress,
            sensor.Metric,
            sensor.ValueKind,
            sensor.ValueKind == TelemetryValueKind.Boolean ? null : numeric,
            sensor.ValueKind == TelemetryValueKind.Boolean ? MeshSeeder.SampleBool(sensor, _tick + 400) : null,
            sensor.Unit,
            _tick));
    }

    private void ToggleSilence()
    {
        _silenced.Clear();
        var candidates = store.Sensors.Keys.OrderBy(_ => Random.Shared.Next()).Take(2);
        foreach (var mac in candidates)
        {
            _silenced.Add(mac);
        }

        logger.LogDebug("Disconnect drill silencing {Macs}", string.Join(", ", _silenced));
    }
}
