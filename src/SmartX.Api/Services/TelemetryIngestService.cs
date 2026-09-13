using Microsoft.AspNetCore.SignalR;
using SmartX.Api.Contracts;
using SmartX.Api.Hubs;
using SmartX.Domain.Alerting;
using SmartX.Domain.Sensors;
using SmartX.Domain.Telemetry;

namespace SmartX.Api.Services;

public sealed class TelemetryIngestService(GatewayStore store, IHubContext<TelemetryHub> hub)
{
    public TelemetrySampleDto IngestFloat(TelemetryPacket<float> packet)
    {
        lock (store.PacketLock)
        {
            store.FloatPackets.Add(packet);
            Trim(store.FloatPackets);
        }

        return Publish(packet.DeviceId, packet.PacketId, packet.Metric, packet.Unit, TelemetryValueKind.Float,
            packet.Payload, null, packet.Timestamp, packet.Sequence);
    }

    public TelemetrySampleDto IngestInt(TelemetryPacket<int> packet)
    {
        lock (store.PacketLock)
        {
            store.IntPackets.Add(packet);
            Trim(store.IntPackets);
        }

        return Publish(packet.DeviceId, packet.PacketId, packet.Metric, packet.Unit, TelemetryValueKind.Integer,
            packet.Payload, null, packet.Timestamp, packet.Sequence);
    }

    public TelemetrySampleDto IngestBool(TelemetryPacket<bool> packet)
    {
        lock (store.PacketLock)
        {
            store.BoolPackets.Add(packet);
            Trim(store.BoolPackets);
        }

        return Publish(packet.DeviceId, packet.PacketId, packet.Metric, packet.Unit, TelemetryValueKind.Boolean,
            null, packet.Payload, packet.Timestamp, packet.Sequence);
    }

    public TelemetrySampleDto Ingest(TelemetryIngestRequest request)
    {
        if (!store.Sensors.TryGetValue(request.DeviceId, out var sensor)
            && !store.Sensors.Values.Any(s => s.MacAddress.Equals(request.DeviceId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Unknown device '{request.DeviceId}'. Register the sensor first.");
        }

        sensor ??= store.Sensors.Values.First(s => s.MacAddress.Equals(request.DeviceId, StringComparison.OrdinalIgnoreCase));
        var deviceId = sensor.MacAddress;
        var metric = string.IsNullOrWhiteSpace(request.Metric) ? sensor.Metric : request.Metric;
        var unit = request.Unit ?? sensor.Unit;
        var sequence = request.Sequence ?? Environment.TickCount;

        return request.ValueKind switch
        {
            TelemetryValueKind.Float => IngestFloat(new TelemetryPacket<float>
            {
                DeviceId = deviceId,
                Metric = metric,
                Payload = (float)(request.NumericValue ?? 0),
                Unit = unit,
                Sequence = sequence
            }),
            TelemetryValueKind.Integer => IngestInt(new TelemetryPacket<int>
            {
                DeviceId = deviceId,
                Metric = metric,
                Payload = (int)Math.Round(request.NumericValue ?? 0),
                Unit = unit,
                Sequence = sequence
            }),
            TelemetryValueKind.Boolean => IngestBool(new TelemetryPacket<bool>
            {
                DeviceId = deviceId,
                Metric = metric,
                Payload = request.BooleanValue ?? ((request.NumericValue ?? 0) > 0),
                Unit = unit,
                Sequence = sequence
            }),
            _ => throw new ArgumentOutOfRangeException(nameof(request.ValueKind))
        };
    }

    public IReadOnlyList<TelemetrySampleDto> History(string? deviceId, int take = 240)
    {
        var now = DateTimeOffset.UtcNow;
        List<TelemetrySampleDto> samples;
        lock (store.PacketLock)
        {
            samples = Enumerate(now)
                .Where(s => deviceId is null || s.DeviceId.Equals(deviceId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(s => s.Timestamp)
                .Take(take)
                .Reverse()
                .ToList();
        }

        return samples;
    }

    public IReadOnlyList<SensorLiveDto> Snapshot()
    {
        var now = DateTimeOffset.UtcNow;
        var history = History(null, 4_000);
        return store.Sensors.Values
            .OrderBy(s => s.Location)
            .ThenBy(s => s.DisplayName)
            .Select(sensor =>
            {
                var spark = history.Where(h => h.DeviceId.Equals(sensor.MacAddress, StringComparison.OrdinalIgnoreCase))
                    .TakeLast(48)
                    .ToList();
                var latest = spark.LastOrDefault() ?? BuildStalePlaceholder(sensor, now);
                return new SensorLiveDto(
                    ToDto(sensor),
                    latest,
                    spark,
                    latest.Severity,
                    latest.ColourHex);
            })
            .ToList();
    }

    private TelemetrySampleDto Publish(
        string deviceId,
        Guid packetId,
        string metric,
        string? unit,
        TelemetryValueKind kind,
        double? numeric,
        bool? boolean,
        DateTimeOffset timestamp,
        int sequence)
    {
        store.LastSeen[deviceId] = timestamp;
        var sample = Project(deviceId, packetId, metric, unit, kind, numeric, boolean, timestamp, sequence, DateTimeOffset.UtcNow);
        store.LatestSeverity[deviceId] = sample.Severity;
        _ = hub.Clients.All.SendAsync("telemetry", sample);
        if (sample.Severity is AlertSeverity.Critical or AlertSeverity.Disconnected)
        {
            _ = hub.Clients.All.SendAsync("alert", sample);
        }

        return sample;
    }

    private TelemetrySampleDto Project(
        string deviceId,
        Guid packetId,
        string metric,
        string? unit,
        TelemetryValueKind kind,
        double? numeric,
        bool? boolean,
        DateTimeOffset timestamp,
        int sequence,
        DateTimeOffset now)
    {
        store.Sensors.TryGetValue(deviceId, out var sensor);
        var lastSeen = store.LastSeen.TryGetValue(deviceId, out var seen) ? seen : timestamp;
        var disconnectAfter = sensor?.DisconnectAfter ?? TimeSpan.FromSeconds(12);
        var stale = ThresholdPalette.IsStale(lastSeen, disconnectAfter, now);
        var seconds = Math.Max(0, (now - lastSeen).TotalSeconds);

        AlertSeverity severity;
        if (kind == TelemetryValueKind.Boolean)
        {
            severity = ThresholdPalette.EvaluateBoolean(boolean ?? false, expectedEnergised: true, stale);
        }
        else
        {
            var thresholds = new SensorThresholds(
                sensor?.WarningLow ?? 0,
                sensor?.WarningHigh ?? 100,
                sensor?.CriticalLow ?? -10,
                sensor?.CriticalHigh ?? 120);
            severity = ThresholdPalette.EvaluateNumeric(numeric ?? 0, thresholds, stale);
        }

        return new TelemetrySampleDto(
            packetId,
            deviceId,
            sensor?.DisplayName ?? deviceId,
            sensor?.Location ?? "Unknown",
            metric,
            unit,
            kind,
            numeric,
            boolean,
            timestamp,
            sequence,
            severity,
            ThresholdPalette.ToLabel(severity),
            ThresholdPalette.ToCss(severity),
            stale,
            Math.Round(seconds, 1));
    }

    private IEnumerable<TelemetrySampleDto> Enumerate(DateTimeOffset now)
    {
        foreach (var p in store.FloatPackets)
        {
            yield return Project(p.DeviceId, p.PacketId, p.Metric, p.Unit, TelemetryValueKind.Float,
                p.Payload, null, p.Timestamp, p.Sequence, now);
        }

        foreach (var p in store.IntPackets)
        {
            yield return Project(p.DeviceId, p.PacketId, p.Metric, p.Unit, TelemetryValueKind.Integer,
                p.Payload, null, p.Timestamp, p.Sequence, now);
        }

        foreach (var p in store.BoolPackets)
        {
            yield return Project(p.DeviceId, p.PacketId, p.Metric, p.Unit, TelemetryValueKind.Boolean,
                null, p.Payload, p.Timestamp, p.Sequence, now);
        }
    }

    private TelemetrySampleDto BuildStalePlaceholder(SensorProfile sensor, DateTimeOffset now)
        => Project(sensor.MacAddress, Guid.Empty, sensor.Metric, sensor.Unit, sensor.ValueKind,
            0, false, now - TimeSpan.FromMinutes(5), 0, now);

    public static SensorDto ToDto(SensorProfile sensor) => new(
        sensor.Id,
        sensor.MacAddress,
        sensor.DisplayName,
        sensor.Location,
        sensor.Category,
        sensor.Metric,
        sensor.Unit,
        sensor.ValueKind,
        sensor.DeploymentPath,
        sensor.Attachments.Select(a => new AttachmentDto(a.Id, a.FileName, a.ContentType, a.ByteSize, a.UploadedAt)).ToList());

    private static void Trim<T>(List<T> list)
    {
        var overflow = list.Count - GatewayStore.MaxPacketsPerType;
        if (overflow > 0)
        {
            list.RemoveRange(0, overflow);
        }
    }
}
