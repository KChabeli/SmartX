using Microsoft.AspNetCore.SignalR;
using SmartX.Api.Contracts;
using SmartX.Api.Hubs;
using SmartX.Domain.Alerting;
using SmartX.Domain.Processing;
using SmartX.Domain.Sensors;
using SmartX.Domain.Telemetry;

namespace SmartX.Api.Services;

/// <summary>
/// Volatile real-time fabric: FIFO queue, critical priority queue, undo stack,
/// O(1) device dictionary, timestamp-sorted log, and unique fault HashSet.
/// </summary>
public sealed class RealtimeMeshEngine(IHubContext<TelemetryHub> hub)
{
    private readonly object _gate = new();
    private readonly Queue<QueuedTelemetry> _standardQueue = new();
    private readonly PriorityQueue<QueuedTelemetry, int> _priorityQueue = new();
    private readonly Stack<DeviceCommand> _undoStack = new();
    private readonly Dictionary<string, LiveDeviceState> _devices = new(StringComparer.OrdinalIgnoreCase);
    private readonly SortedDictionary<LogKey, TelemetrySampleDto> _timeline = [];
    private readonly HashSet<ActiveFault> _activeFaults = [];
    private readonly HashSet<string> _disconnected = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<OperatorEvent> _operatorWindow = new();
    private readonly Dictionary<(string Context, string Action), int> _associations = [];

    public const int MaxTimeline = 8_000;
    public const int OperatorWindowSize = 48;

    public int StandardEnqueued { get; private set; }
    public int PriorityEnqueued { get; private set; }
    public int StandardProcessed { get; private set; }
    public int PriorityProcessed { get; private set; }

    public static readonly Dictionary<string, string> RelatedActuators = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AA:7C:3F:10:00:01"] = "AA:7C:3F:10:00:08",
        ["AA:7C:3F:10:00:04"] = "AA:7C:3F:10:00:06",
        ["AA:7C:3F:10:00:05"] = "AA:7C:3F:10:00:07",
        ["AA:7C:3F:20:00:11"] = "AA:7C:3F:10:00:08",
        ["BB:91:44:20:10:05"] = "BB:91:44:20:10:06",
        ["CC:18:90:30:00:21"] = "CC:18:90:30:00:24",
        ["CC:18:90:30:00:22"] = "CC:18:90:30:00:24",
        ["CC:18:90:30:00:23"] = "CC:18:90:30:00:24"
    };

    public void RegisterDevice(SensorProfile profile)
    {
        lock (_gate)
        {
            _devices[profile.MacAddress] = new LiveDeviceState(profile);
        }
    }

    public bool TryGetDevice(string mac, out LiveDeviceState state)
    {
        lock (_gate)
        {
            return _devices.TryGetValue(mac, out state!);
        }
    }

    public QueuedTelemetry Enqueue(TelemetryIngestRequest request, SensorProfile sensor)
    {
        var numeric = request.NumericValue;
        var boolean = request.BooleanValue;
        var kind = request.ValueKind;
        var metric = string.IsNullOrWhiteSpace(request.Metric) ? sensor.Metric : request.Metric;
        var critical = IsCritical(sensor, kind, numeric, boolean);
        var envelope = new QueuedTelemetry
        {
            DeviceId = sensor.MacAddress,
            Metric = metric,
            ValueKind = kind,
            NumericValue = numeric,
            BooleanValue = boolean,
            Unit = request.Unit ?? sensor.Unit,
            Sequence = request.Sequence ?? Environment.TickCount,
            Lane = critical ? IngestLane.Priority : IngestLane.Standard,
            Priority = critical ? 0 : 10
        };

        lock (_gate)
        {
            if (critical)
            {
                _priorityQueue.Enqueue(envelope, envelope.Priority);
                PriorityEnqueued++;
            }
            else
            {
                _standardQueue.Enqueue(envelope);
                StandardEnqueued++;
            }
        }

        return envelope;
    }

    public bool TryDequeueNext(out QueuedTelemetry envelope)
    {
        lock (_gate)
        {
            if (_priorityQueue.Count > 0)
            {
                envelope = _priorityQueue.Dequeue();
                PriorityProcessed++;
                return true;
            }

            if (_standardQueue.Count > 0)
            {
                envelope = _standardQueue.Dequeue();
                StandardProcessed++;
                return true;
            }
        }

        envelope = null!;
        return false;
    }

    public void ObserveCommitted(TelemetrySampleDto sample)
    {
        lock (_gate)
        {
            if (_devices.TryGetValue(sample.DeviceId, out var live))
            {
                live.Latest = sample;
                live.Severity = sample.Severity;
                live.LastSeen = sample.Timestamp;
                if (sample.ValueKind == TelemetryValueKind.Boolean)
                {
                    live.ActuatorEnergised = sample.BooleanValue;
                }
            }

            _timeline[new LogKey(sample.Timestamp, sample.PacketId)] = sample;
            while (_timeline.Count > MaxTimeline)
            {
                _timeline.Remove(_timeline.Keys.First());
            }

            ApplyFaultSet(sample);
        }
    }

    public void RefreshDisconnects(DateTimeOffset now)
    {
        lock (_gate)
        {
            foreach (var live in _devices.Values)
            {
                var stale = now - live.LastSeen >= live.Profile.DisconnectAfter;
                var fault = new ActiveFault(live.Profile.MacAddress, "DISCONNECT");
                if (stale)
                {
                    if (_disconnected.Add(live.Profile.MacAddress) && _activeFaults.Add(fault))
                    {
                        live.Severity = AlertSeverity.Disconnected;
                    }
                }
                else
                {
                    _disconnected.Remove(live.Profile.MacAddress);
                    _activeFaults.Remove(fault);
                }
            }
        }
    }

    public DeviceCommand IssueCommand(string mac, CommandVerb verb, string reason)
    {
        lock (_gate)
        {
            if (!_devices.TryGetValue(mac, out var live))
            {
                throw new InvalidOperationException($"Unknown device '{mac}'.");
            }

            if (live.Profile.Category != SensorCategory.Actuator)
            {
                throw new InvalidOperationException("Overrides can only target actuators (pumps, valves, relays).");
            }

            var previous = live.ActuatorEnergised ?? true;
            var next = verb == CommandVerb.Energise;
            var command = new DeviceCommand
            {
                TargetMac = live.Profile.MacAddress,
                TargetName = live.Profile.DisplayName,
                Verb = verb,
                PreviousState = previous,
                NewState = next,
                Reason = reason
            };
            _undoStack.Push(command);
            RecordAssociationUnsafe($"command|{verb}|{mac}");
            return command;
        }
    }

    public DeviceCommand? UndoLast()
    {
        lock (_gate)
        {
            if (_undoStack.Count == 0)
            {
                return null;
            }

            var last = _undoStack.Pop();
            last.WasUndone = true;
            return last.Invert();
        }
    }

    public IReadOnlyList<DeviceCommand> CommandHistory()
    {
        lock (_gate)
        {
            return _undoStack.ToArray();
        }
    }

    public void RecordSearch(string query)
    {
        lock (_gate)
        {
            RecordAssociationUnsafe($"search|{query.Trim()}");
        }
    }

    public void SeedAssociationPriors()
    {
        lock (_gate)
        {
            Boost("soil_moisture|Critical", "search|Pump A", 14);
            Boost("soil_moisture|Critical", "command|Energise|AA:7C:3F:10:00:08", 12);
            Boost("ph|Critical", "command|Energise|AA:7C:3F:10:00:07", 8);
            Boost("wattage|Critical", "command|DeEnergise|CC:18:90:30:00:24", 11);
            Boost("tank_level|Critical", "command|DeEnergise|BB:91:44:20:10:06", 7);
            Boost("temperature|Disconnected", "search|Fogger", 6);
        }
    }

    public IReadOnlyList<InsightDto> BuildInsights()
    {
        lock (_gate)
        {
            var insights = new List<InsightDto>();
            var faultContexts = _devices.Values
                .Where(d => d.Severity is AlertSeverity.Critical or AlertSeverity.Disconnected or AlertSeverity.Warning)
                .Select(d => (Device: d, Context: $"{d.Profile.Metric}|{d.Severity}"))
                .ToList();

            foreach (var (device, context) in faultContexts)
            {
                var matches = _associations
                    .Where(kv => kv.Key.Context.Equals(context, StringComparison.OrdinalIgnoreCase)
                                 && kv.Key.Action.StartsWith("command|", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(kv => kv.Value)
                    .Take(2);

                var contextTotal = _associations
                    .Where(kv => kv.Key.Context.Equals(context, StringComparison.OrdinalIgnoreCase))
                    .Sum(kv => kv.Value);

                foreach (var match in matches)
                {
                    var parts = match.Key.Action.Split('|');
                    if (parts.Length < 3 || !Enum.TryParse<CommandVerb>(parts[1], out var verb))
                    {
                        continue;
                    }

                    var targetMac = parts[2];
                    if (!_devices.TryGetValue(targetMac, out var target))
                    {
                        continue;
                    }

                    var confidence = contextTotal == 0 ? 0 : Math.Round(100.0 * match.Value / contextTotal, 1);
                    insights.Add(new InsightDto(
                        Guid.NewGuid(),
                        $"{verb} {target.Profile.DisplayName}",
                        $"Operators historically followed {device.Profile.DisplayName} {device.Severity} with this override ({match.Value} of {contextTotal} events). Context-conditioned association, not a static if-then rule.",
                        confidence,
                        target.Profile.MacAddress,
                        target.Profile.DisplayName,
                        verb.ToString(),
                        device.Profile.MacAddress,
                        device.Profile.DisplayName,
                        context));
                }
            }

            return insights
                .OrderByDescending(i => i.Confidence)
                .Take(6)
                .ToList();
        }
    }

    public RealtimeDiagnosticsDto Diagnostics()
    {
        lock (_gate)
        {
            return new RealtimeDiagnosticsDto(
                _standardQueue.Count,
                _priorityQueue.Count,
                _undoStack.Count,
                _devices.Count,
                _timeline.Count,
                _activeFaults.Count,
                _disconnected.Count,
                StandardEnqueued,
                PriorityEnqueued,
                StandardProcessed,
                PriorityProcessed,
                _activeFaults.Select(f => f.ToString()).OrderBy(x => x).ToList(),
                _disconnected.OrderBy(x => x).ToList());
        }
    }

    public IReadOnlyList<TelemetrySampleDto> Timeline(string? deviceId, int take)
    {
        lock (_gate)
        {
            IEnumerable<KeyValuePair<LogKey, TelemetrySampleDto>> query = _timeline;
            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                query = query.Where(kv => kv.Value.DeviceId.Equals(deviceId, StringComparison.OrdinalIgnoreCase));
            }

            return query.TakeLast(take).Select(kv => kv.Value).ToList();
        }
    }

    public IReadOnlyList<LiveDeviceState> SnapshotDevices()
    {
        lock (_gate)
        {
            return _devices.Values.ToList();
        }
    }

    public PriorityBypassDto DemoPriorityBypass()
    {
        var fifo = new Queue<QueuedTelemetry>();
        var urgent = new PriorityQueue<QueuedTelemetry, int>();
        for (var i = 1; i <= 5; i++)
        {
            fifo.Enqueue(new QueuedTelemetry
            {
                DeviceId = $"DEMO-STD-{i}",
                Metric = "wattage",
                ValueKind = TelemetryValueKind.Integer,
                NumericValue = 400,
                Lane = IngestLane.Standard,
                Priority = 10
            });
        }

        urgent.Enqueue(new QueuedTelemetry
        {
            DeviceId = "DEMO-CRIT",
            Metric = "wattage",
            ValueKind = TelemetryValueKind.Integer,
            NumericValue = 14000,
            Lane = IngestLane.Priority,
            Priority = 0
        }, 0);

        var order = new List<string>();
        while (urgent.Count > 0 || fifo.Count > 0)
        {
            if (urgent.Count > 0)
            {
                var next = urgent.Dequeue();
                order.Add($"{next.Lane}:{next.DeviceId}");
            }
            else
            {
                var next = fifo.Dequeue();
                order.Add($"{next.Lane}:{next.DeviceId}");
            }
        }

        return new PriorityBypassDto(
            "Five FIFO packets were already queued. A critical envelope then arrived. Drain always pops PriorityQueue<T> before Queue<T>, so DEMO-CRIT is processed first.",
            order);
    }

    public async Task BroadcastQueuesAsync()
    {
        var dto = Diagnostics();
        await hub.Clients.All.SendAsync("queues", dto);
    }

    public async Task BroadcastCommandAsync(DeviceCommand command)
    {
        await hub.Clients.All.SendAsync("command", ToDto(command));
    }

    public static CommandDto ToDto(DeviceCommand command) => new(
        command.CommandId,
        command.TargetMac,
        command.TargetName,
        command.Verb.ToString(),
        command.PreviousState,
        command.NewState,
        command.IssuedAt,
        command.Reason,
        command.WasUndone);

    private void ApplyFaultSet(TelemetrySampleDto sample)
    {
        var code = sample.Severity switch
        {
            AlertSeverity.Critical => "SPIKE",
            AlertSeverity.Disconnected => "DISCONNECT",
            AlertSeverity.Warning => "WARN",
            _ => null
        };

        foreach (var stale in new[] { "SPIKE", "DISCONNECT", "WARN" })
        {
            if (code != stale)
            {
                _activeFaults.Remove(new ActiveFault(sample.DeviceId, stale));
            }
        }

        if (code is null)
        {
            _disconnected.Remove(sample.DeviceId);
            return;
        }

        var fault = new ActiveFault(sample.DeviceId, code);
        _activeFaults.Add(fault);
        if (code == "DISCONNECT")
        {
            _disconnected.Add(sample.DeviceId);
        }
        else
        {
            _disconnected.Remove(sample.DeviceId);
        }
    }

    private void RecordAssociationUnsafe(string action)
    {
        var ev = new OperatorEvent
        {
            Kind = action.StartsWith("search", StringComparison.Ordinal) ? "search" : "command",
            Payload = action,
            Context = CurrentContextUnsafe(),
            At = DateTimeOffset.UtcNow
        };
        _operatorWindow.Enqueue(ev);
        while (_operatorWindow.Count > OperatorWindowSize)
        {
            _operatorWindow.Dequeue();
        }

        if (ev.Context is not null)
        {
            var key = (ev.Context, action);
            _associations[key] = _associations.GetValueOrDefault(key) + 1;
        }
    }

    private string? CurrentContextUnsafe()
    {
        var hot = _devices.Values
            .Where(d => d.Severity is AlertSeverity.Critical or AlertSeverity.Disconnected)
            .OrderByDescending(d => d.Severity)
            .ThenByDescending(d => d.LastSeen)
            .FirstOrDefault();
        return hot is null ? null : $"{hot.Profile.Metric}|{hot.Severity}";
    }

    private void Boost(string context, string action, int count)
        => _associations[(context, action)] = _associations.GetValueOrDefault((context, action)) + count;

    private static bool IsCritical(SensorProfile sensor, TelemetryValueKind kind, double? numeric, bool? boolean)
    {
        if (kind == TelemetryValueKind.Boolean)
        {
            return false;
        }

        var value = numeric ?? 0;
        var thresholds = new SensorThresholds(
            sensor.WarningLow ?? 0,
            sensor.WarningHigh ?? 100,
            sensor.CriticalLow ?? -10,
            sensor.CriticalHigh ?? 120);
        return ThresholdPalette.EvaluateNumeric(value, thresholds, isStale: false) == AlertSeverity.Critical;
    }
}

public sealed class LiveDeviceState(SensorProfile profile)
{
    public SensorProfile Profile { get; } = profile;
    public AlertSeverity Severity { get; set; } = AlertSeverity.Nominal;
    public bool? ActuatorEnergised { get; set; }
    public DateTimeOffset LastSeen { get; set; } = DateTimeOffset.UtcNow;
    public TelemetrySampleDto? Latest { get; set; }
}
