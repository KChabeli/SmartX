using SmartX.Domain.Alerting;
using SmartX.Domain.Deployment;
using SmartX.Domain.Sensors;
using SmartX.Domain.Telemetry;

namespace SmartX.Api.Contracts;

public sealed record PillarDto(string Id, string Title, string Summary, bool Enabled, string? DeferredUntil);

public sealed record RegisterSensorRequest(
    string MacAddress,
    string DisplayName,
    string Location,
    SensorCategory Category,
    string Metric,
    string Unit,
    TelemetryValueKind ValueKind,
    double? WarningLow,
    double? WarningHigh,
    double? CriticalLow,
    double? CriticalHigh,
    string? DeploymentPath);

public sealed record SensorDto(
    Guid Id,
    string MacAddress,
    string DisplayName,
    string Location,
    SensorCategory Category,
    string Metric,
    string Unit,
    TelemetryValueKind ValueKind,
    string DeploymentPath,
    IReadOnlyList<AttachmentDto> Attachments);

public sealed record AttachmentDto(Guid Id, string FileName, string ContentType, long ByteSize, DateTimeOffset UploadedAt);

public sealed record TelemetryIngestRequest(
    string DeviceId,
    string Metric,
    TelemetryValueKind ValueKind,
    double? NumericValue,
    bool? BooleanValue,
    string? Unit,
    int? Sequence);

public sealed record TelemetrySampleDto(
    Guid PacketId,
    string DeviceId,
    string DisplayName,
    string Location,
    string Metric,
    string? Unit,
    TelemetryValueKind ValueKind,
    double? NumericValue,
    bool? BooleanValue,
    DateTimeOffset Timestamp,
    int Sequence,
    AlertSeverity Severity,
    string SeverityLabel,
    string ColourHex,
    bool IsStale,
    double SecondsSinceLastSeen);

public sealed record SensorLiveDto(
    SensorDto Sensor,
    TelemetrySampleDto? Latest,
    IReadOnlyList<TelemetrySampleDto> Sparkline,
    AlertSeverity Severity,
    string ColourHex);

public sealed record MeterAggregateRequest(string LeftId, int LeftWatts, float LeftPf, string RightId, int RightWatts, float RightPf);

public sealed record MeterAggregateDto(string Expression, int CombinedWatts, float CombinedPowerFactor, bool LeftExceedsRight);

public sealed record DeploymentValidateRequest(DeploymentNodeDto Root, string? RequiredMac);

public sealed record DeploymentNodeDto(
    string Id,
    string Name,
    DeploymentKind Kind,
    string? MacAddress,
    List<DeploymentNodeDto>? Children);

public sealed record DeploymentValidateDto(bool IsSafe, IReadOnlyList<string> Errors, string ResolvedPath, IReadOnlyList<string>? AncestorChain);

public sealed record IssueCommandRequest(string TargetMac, string Verb, string? Reason);

public sealed record CommandDto(
    Guid CommandId,
    string TargetMac,
    string TargetName,
    string Verb,
    bool PreviousState,
    bool NewState,
    DateTimeOffset IssuedAt,
    string Reason,
    bool WasUndone);

public sealed record InsightDto(
    Guid Id,
    string Title,
    string Rationale,
    double Confidence,
    string TargetMac,
    string TargetName,
    string Verb,
    string ContextMac,
    string ContextName,
    string Context);

public sealed record RealtimeDiagnosticsDto(
    int StandardQueueDepth,
    int PriorityQueueDepth,
    int UndoStackDepth,
    int DictionarySize,
    int SortedLogCount,
    int UniqueFaults,
    int DisconnectedNodes,
    int StandardEnqueued,
    int PriorityEnqueued,
    int StandardProcessed,
    int PriorityProcessed,
    IReadOnlyList<string> ActiveFaults,
    IReadOnlyList<string> DisconnectedMacs);

public sealed record PriorityBypassDto(string Explanation, IReadOnlyList<string> ProcessingOrder);

public sealed record DeviceSearchRequest(string? Query, string? Category, string? Severity);
