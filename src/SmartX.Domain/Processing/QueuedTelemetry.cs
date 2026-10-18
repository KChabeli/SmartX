using SmartX.Domain.Telemetry;

namespace SmartX.Domain.Processing;

public enum IngestLane
{
    Standard = 0,
    Priority = 1
}

/// <summary>
/// Envelope sitting on either the FIFO <see cref="Queue{T}"/> or the critical
/// <see cref="PriorityQueue{TElement, TPriority}"/> before the packet is committed.
/// </summary>
public sealed class QueuedTelemetry
{
    public Guid EnvelopeId { get; init; } = Guid.NewGuid();
    public required string DeviceId { get; init; }
    public required string Metric { get; init; }
    public required TelemetryValueKind ValueKind { get; init; }
    public double? NumericValue { get; init; }
    public bool? BooleanValue { get; init; }
    public string? Unit { get; init; }
    public int Sequence { get; init; }
    public DateTimeOffset EnqueuedAt { get; init; } = DateTimeOffset.UtcNow;
    public IngestLane Lane { get; init; }
    public int Priority { get; init; }
}

public readonly record struct LogKey(DateTimeOffset Timestamp, Guid PacketId) : IComparable<LogKey>
{
    public int CompareTo(LogKey other)
    {
        var time = Timestamp.CompareTo(other.Timestamp);
        return time != 0 ? time : PacketId.CompareTo(other.PacketId);
    }
}

public readonly record struct ActiveFault(string DeviceId, string FaultCode)
{
    public override string ToString() => $"{DeviceId}|{FaultCode}";
}
