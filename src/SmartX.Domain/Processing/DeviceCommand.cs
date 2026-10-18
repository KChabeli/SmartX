namespace SmartX.Domain.Processing;

public enum CommandVerb
{
    Energise = 0,
    DeEnergise = 1
}

public sealed class DeviceCommand
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public required string TargetMac { get; init; }
    public required string TargetName { get; init; }
    public required CommandVerb Verb { get; init; }
    public bool PreviousState { get; init; }
    public bool NewState { get; init; }
    public DateTimeOffset IssuedAt { get; init; } = DateTimeOffset.UtcNow;
    public string Reason { get; init; } = "manual-override";
    public bool WasUndone { get; set; }

    public DeviceCommand Invert() => new()
    {
        TargetMac = TargetMac,
        TargetName = TargetName,
        Verb = NewState ? CommandVerb.DeEnergise : CommandVerb.Energise,
        PreviousState = NewState,
        NewState = PreviousState,
        Reason = "undo",
        IssuedAt = DateTimeOffset.UtcNow
    };
}

public sealed class OperatorEvent
{
    public required string Kind { get; init; }
    public required string Payload { get; init; }
    public string? Context { get; init; }
    public DateTimeOffset At { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class AssociationRule
{
    public required string Context { get; init; }
    public required string Action { get; init; }
    public int Count { get; set; }
}
