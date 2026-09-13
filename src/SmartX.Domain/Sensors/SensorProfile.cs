using SmartX.Domain.Telemetry;

namespace SmartX.Domain.Sensors;

public enum SensorCategory
{
    Environmental = 0,
    PowerConsumption = 1,
    Actuator = 2
}

public sealed class SensorProfile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string MacAddress { get; init; }
    public required string DisplayName { get; init; }
    public required string Location { get; init; }
    public required SensorCategory Category { get; init; }
    public required string Metric { get; init; }
    public required string Unit { get; init; }
    public required TelemetryValueKind ValueKind { get; init; }
    public double? WarningLow { get; init; }
    public double? WarningHigh { get; init; }
    public double? CriticalLow { get; init; }
    public double? CriticalHigh { get; init; }
    public TimeSpan ExpectedInterval { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan DisconnectAfter { get; init; } = TimeSpan.FromSeconds(12);
    public string DeploymentPath { get; init; } = "Unassigned";
    public DateTimeOffset RegisteredAt { get; init; } = DateTimeOffset.UtcNow;
    public List<SensorAttachment> Attachments { get; } = [];
}

public sealed class SensorAttachment
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required string StoredPath { get; init; }
    public long ByteSize { get; init; }
    public DateTimeOffset UploadedAt { get; init; } = DateTimeOffset.UtcNow;
}
