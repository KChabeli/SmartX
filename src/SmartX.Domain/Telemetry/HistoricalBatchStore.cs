namespace SmartX.Domain.Telemetry;

/// <summary>
/// Holds sequential historical windows in a jagged array (<c>T[][]</c>) before compacting
/// them into an optimised <see cref="List{T}"/>. Each inner array is one raw sampling burst
/// from a microcontroller (for example 60 samples at 1 Hz).
/// </summary>
public sealed class HistoricalBatchStore<T> where T : struct, IComparable<T>
{
    private T[][] _windows;
    private int _count;

    public HistoricalBatchStore(int initialCapacity = 8)
    {
        _windows = new T[initialCapacity][];
    }

    public int WindowCount => _count;

    public void AddWindow(T[] samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (_count == _windows.Length)
        {
            Array.Resize(ref _windows, _windows.Length * 2);
        }

        _windows[_count++] = samples;
    }

    /// <summary>
    /// Compacts jagged raw windows into a typed packet list without boxing value payloads.
    /// </summary>
    public List<TelemetryPacket<T>> FlushToCollection(
        string deviceId,
        string metric,
        string unit,
        DateTimeOffset windowStart,
        TimeSpan sampleInterval)
    {
        var packets = new List<TelemetryPacket<T>>();
        var sequence = 0;
        var cursor = windowStart;

        for (var w = 0; w < _count; w++)
        {
            var window = _windows[w];
            for (var i = 0; i < window.Length; i++)
            {
                packets.Add(new TelemetryPacket<T>
                {
                    DeviceId = deviceId,
                    Metric = metric,
                    Payload = window[i],
                    Timestamp = cursor,
                    Sequence = sequence++,
                    Unit = unit,
                    Quality = QualityFlag.Good
                });
                cursor += sampleInterval;
            }
        }

        return packets;
    }

    public T[][] SnapshotWindows()
    {
        var copy = new T[_count][];
        Array.Copy(_windows, copy, _count);
        return copy;
    }
}

/// <summary>
/// Rectangular zone-by-minute heat grid used by the colour-coded threshold dashboard.
/// </summary>
public sealed class ZoneHeatGrid
{
    private readonly AlertSeverity[,] _cells;

    public int ZoneCount { get; }
    public int MinuteBuckets { get; }

    public ZoneHeatGrid(int zoneCount, int minuteBuckets)
    {
        ZoneCount = zoneCount;
        MinuteBuckets = minuteBuckets;
        _cells = new AlertSeverity[zoneCount, minuteBuckets];
    }

    public AlertSeverity this[int zone, int minute]
    {
        get => _cells[zone, minute];
        set => _cells[zone, minute] = value;
    }

    public AlertSeverity WorstInZone(int zone)
    {
        var worst = AlertSeverity.Nominal;
        for (var m = 0; m < MinuteBuckets; m++)
        {
            if (_cells[zone, m] > worst)
            {
                worst = _cells[zone, m];
            }
        }

        return worst;
    }
}

public enum AlertSeverity
{
    Nominal = 0,
    Warning = 1,
    Disconnected = 2,
    Critical = 3
}
