using System.Collections.Concurrent;
using SmartX.Domain.Alerting;
using SmartX.Domain.Sensors;
using SmartX.Domain.Telemetry;

namespace SmartX.Api.Services;

/// <summary>
/// In-memory gateway store. Typed packet lists keep float/int/bool payloads unboxed.
/// </summary>
public sealed class GatewayStore
{
    public ConcurrentDictionary<string, SensorProfile> Sensors { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<TelemetryPacket<float>> FloatPackets { get; } = [];
    public List<TelemetryPacket<int>> IntPackets { get; } = [];
    public List<TelemetryPacket<bool>> BoolPackets { get; } = [];

    public ConcurrentDictionary<string, DateTimeOffset> LastSeen { get; } = new(StringComparer.OrdinalIgnoreCase);
    public ConcurrentDictionary<string, AlertSeverity> LatestSeverity { get; } = new(StringComparer.OrdinalIgnoreCase);

    public readonly object PacketLock = new();

    public const int MaxPacketsPerType = 20_000;
}
