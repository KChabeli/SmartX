namespace SmartX.Domain.Telemetry;

/// <summary>
/// Generic, boxing-free wrapper for ESP32 telemetry of any value type.
/// Constraining <typeparamref name="T"/> to <see cref="struct"/> keeps packets in typed
/// collections without boxing floats, ints, or bools into <see cref="object"/>.
/// </summary>
public sealed class TelemetryPacket<T> where T : struct, IComparable<T>
{
    public Guid PacketId { get; init; } = Guid.NewGuid();
    public required string DeviceId { get; init; }
    public required string Metric { get; init; }
    public required T Payload { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public int Sequence { get; init; }
    public string? Unit { get; init; }
    public QualityFlag Quality { get; init; } = QualityFlag.Good;

    public bool IsNewerThan(TelemetryPacket<T> other) => Sequence > other.Sequence
        || Timestamp > other.Timestamp;
}

public enum QualityFlag
{
    Good = 0,
    Uncertain = 1,
    Bad = 2,
    Stale = 3
}

public enum TelemetryValueKind
{
    Float = 0,
    Integer = 1,
    Boolean = 2
}
