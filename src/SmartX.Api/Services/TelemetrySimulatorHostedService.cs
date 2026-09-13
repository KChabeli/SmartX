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
                if (_silenced.Contains(sensor.MacAddress))
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

            await Task.Delay(TimeSpan.FromMilliseconds(1600), stoppingToken);
        }
    }

    private void Publish(Domain.Sensors.SensorProfile sensor)
    {
        switch (sensor.ValueKind)
        {
            case TelemetryValueKind.Float:
                ingest.IngestFloat(new TelemetryPacket<float>
                {
                    DeviceId = sensor.MacAddress,
                    Metric = sensor.Metric,
                    Payload = (float)MeshSeeder.SampleNumeric(sensor, _tick + 400),
                    Unit = sensor.Unit,
                    Sequence = _tick
                });
                break;
            case TelemetryValueKind.Integer:
                ingest.IngestInt(new TelemetryPacket<int>
                {
                    DeviceId = sensor.MacAddress,
                    Metric = sensor.Metric,
                    Payload = (int)Math.Round(MeshSeeder.SampleNumeric(sensor, _tick + 400)),
                    Unit = sensor.Unit,
                    Sequence = _tick
                });
                break;
            default:
                ingest.IngestBool(new TelemetryPacket<bool>
                {
                    DeviceId = sensor.MacAddress,
                    Metric = sensor.Metric,
                    Payload = MeshSeeder.SampleBool(sensor, _tick + 400),
                    Unit = sensor.Unit,
                    Sequence = _tick
                });
                break;
        }
    }

    private void ToggleSilence()
    {
        _silenced.Clear();
        var candidates = store.Sensors.Keys.OrderBy(_ => Random.Shared.Next()).Take(2);
        foreach (var mac in candidates)
        {
            _silenced.Add(mac);
        }

        logger.LogInformation("Disconnect drill silencing {Macs}", string.Join(", ", _silenced));
    }
}
