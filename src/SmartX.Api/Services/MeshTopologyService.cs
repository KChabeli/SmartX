using SmartX.Api.Contracts;
using SmartX.Domain.Mesh;
using SmartX.Domain.Trees;

namespace SmartX.Api.Services;

/// <summary>
/// Part 3 fabric: per-MAC AVL history plus an undirected radio graph.
/// Kruskal MST is the power-efficient hop backbone; BFS from the gateway
/// finds who is still reachable when a node drops off ESP-NOW.
/// </summary>
public sealed class MeshTopologyService(RealtimeMeshEngine engine)
{
    private readonly MeshGraph _graph = new();
    private readonly Dictionary<string, AvlTree<TelemetryKey, TelemetrySampleDto>> _history = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private bool _seeded;

    public void EnsureSeeded(IEnumerable<Domain.Sensors.SensorProfile> sensors)
    {
        lock (_gate)
        {
            if (_seeded)
            {
                return;
            }

            _graph.AddVertex(new MeshVertex(
                MeshGraph.GatewayId,
                "Smart-X Gateway",
                "Core",
                "Ruimsig",
                500,
                36,
                true));

            foreach (var sensor in sensors)
            {
                var (x, y) = Layout(sensor.MacAddress);
                var facility = sensor.Location.Split('/')[0].Trim();
                var zone = sensor.Location.Contains('/') ? sensor.Location.Split('/')[1].Trim() : facility;
                _graph.AddVertex(new MeshVertex(sensor.MacAddress, sensor.DisplayName, facility, zone, x, y, false));
                if (!_history.ContainsKey(sensor.MacAddress))
                {
                    _history[sensor.MacAddress] = new AvlTree<TelemetryKey, TelemetrySampleDto>();
                }
            }

            foreach (var (from, to, cost, radio) in Catalogue)
            {
                _graph.AddUndirectedLink(from, to, cost, radio);
            }

            _seeded = true;
        }
    }

    public void Index(TelemetrySampleDto sample)
    {
        lock (_gate)
        {
            if (!_history.TryGetValue(sample.DeviceId, out var tree))
            {
                tree = new AvlTree<TelemetryKey, TelemetrySampleDto>();
                _history[sample.DeviceId] = tree;
            }

            tree.Insert(new TelemetryKey(sample.Timestamp, sample.PacketId), sample);
        }
    }

    public MeshTopologyDto Snapshot(string? selectedMac)
    {
        var diagnostics = engine.Diagnostics();
        var down = diagnostics.DisconnectedMacs.ToHashSet(StringComparer.OrdinalIgnoreCase);
        BfsWalk bfs;
        MstResult mst;
        IReadOnlyList<string> hopPath;
        IReadOnlyList<MeshHop> alts;
        int treeCount;
        lock (_gate)
        {
            bfs = _graph.BreadthFirst(MeshGraph.GatewayId, down);
            mst = _graph.Kruskal(down);
            hopPath = string.IsNullOrWhiteSpace(selectedMac)
                ? []
                : _graph.PathFromParents(mst.ParentFromGateway, selectedMac);
            alts = string.IsNullOrWhiteSpace(selectedMac)
                ? []
                : _graph.AlternateHops(selectedMac, down);
            treeCount = _history.TryGetValue(selectedMac ?? "", out var tree) ? tree.Count : 0;
        }

        var devices = engine.SnapshotDevices().ToDictionary(d => d.Profile.MacAddress, StringComparer.OrdinalIgnoreCase);
        var vertices = _graph.Vertices.Select(v =>
        {
            devices.TryGetValue(v.Id, out var live);
            var reachable = bfs.Reachable.Contains(v.Id, StringComparer.OrdinalIgnoreCase);
            var isDown = !v.IsGateway && (down.Contains(v.Id) || !reachable);
            return new MeshVertexDto(
                v.Id,
                v.Label,
                v.Facility,
                v.Zone,
                v.X,
                v.Y,
                v.IsGateway,
                isDown ? "Disconnected" : live?.Severity.ToString() ?? "Nominal",
                isDown,
                reachable,
                live?.Profile.Metric ?? "gateway",
                live?.Latest?.ColourHex ?? "#34D399");
        }).ToList();

        var onMst = mst.Edges
            .Select(e => Canonical(e.From, e.To))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var onPath = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i < hopPath.Count; i++)
        {
            onPath.Add(Canonical(hopPath[i - 1], hopPath[i]));
        }

        var links = _graph.Links.Select(l => new MeshLinkDto(
            l.From,
            l.To,
            l.CostMilliJoule,
            l.Radio,
            onMst.Contains(Canonical(l.From, l.To)),
            onPath.Contains(Canonical(l.From, l.To)))).ToList();

        var selectedDown = !string.IsNullOrWhiteSpace(selectedMac) && down.Contains(selectedMac);
        var heal = selectedDown
            ? $"Node {selectedMac} is silent. BFS from the gateway stops at its live neighbours; telemetry should hop {string.Join(" or ", alts.Select(a => a.To))}."
            : hopPath.Count > 0
                ? $"Kruskal MST hop path ({hopPath.Count - 1} hops, cheapest radio joules): {string.Join(" → ", hopPath)}"
                : "Select a MAC to trace its MST path to the gateway.";

        return new MeshTopologyDto(
            vertices,
            links,
            hopPath,
            bfs.VisitOrder,
            bfs.Partitioned,
            mst.Edges.Select(e => new MeshLinkDto(e.From, e.To, e.CostMilliJoule, e.Radio, true, false)).ToList(),
            mst.TotalMilliJoule,
            mst.Algorithm,
            alts.Select(a => new MeshHopDto(a.From, a.To, a.CostMilliJoule)).ToList(),
            heal,
            treeCount,
            diagnostics.DisconnectedMacs);
    }

    public IReadOnlyList<TelemetrySampleDto> Range(string deviceId, DateTimeOffset from, DateTimeOffset to)
    {
        lock (_gate)
        {
            if (!_history.TryGetValue(deviceId, out var tree))
            {
                return [];
            }

            return tree.Range(TelemetryKey.StartOf(from), TelemetryKey.EndOf(to));
        }
    }

    public int IndexedCount(string deviceId)
    {
        lock (_gate)
        {
            return _history.TryGetValue(deviceId, out var tree) ? tree.Count : 0;
        }
    }

    private static string Canonical(string a, string b)
        => string.Compare(a, b, StringComparison.OrdinalIgnoreCase) <= 0 ? $"{a}|{b}" : $"{b}|{a}";

    private static (double X, double Y) Layout(string mac) => mac switch
    {
        "AA:7C:3F:20:00:11" => (168, 118),
        "AA:7C:3F:10:00:01" => (188, 228),
        "AA:7C:3F:10:00:08" => (250, 188),
        "AA:7C:3F:10:00:02" => (110, 278),
        "AA:7C:3F:10:00:03" => (258, 278),
        "AA:7C:3F:10:00:06" => (268, 348),
        "AA:7C:3F:10:00:04" => (176, 368),
        "AA:7C:3F:10:00:05" => (86, 428),
        "AA:7C:3F:10:00:07" => (128, 508),
        "BB:91:44:20:10:04" => (500, 118),
        "BB:91:44:20:10:01" => (500, 236),
        "BB:91:44:20:10:02" => (572, 286),
        "BB:91:44:20:10:03" => (428, 286),
        "BB:91:44:20:10:05" => (456, 366),
        "BB:91:44:20:10:06" => (548, 366),
        "CC:18:90:30:00:21" => (832, 118),
        "CC:18:90:30:00:23" => (772, 228),
        "CC:18:90:30:00:22" => (892, 228),
        "CC:18:90:30:00:24" => (824, 308),
        "CC:18:90:30:00:25" => (912, 318),
        "CC:18:90:30:00:26" => (762, 398),
        _ => (500, 300)
    };

    private static readonly (string From, string To, double Cost, string Radio)[] Catalogue =
    [
        (MeshGraph.GatewayId, "AA:7C:3F:20:00:11", 6.2, "ESP-NOW"),
        (MeshGraph.GatewayId, "BB:91:44:20:10:04", 6.0, "ESP-NOW"),
        (MeshGraph.GatewayId, "CC:18:90:30:00:21", 6.4, "ESP-NOW"),
        ("AA:7C:3F:10:00:01", "AA:7C:3F:10:00:02", 1.8, "ESP-NOW"),
        ("AA:7C:3F:10:00:01", "AA:7C:3F:10:00:03", 1.9, "ESP-NOW"),
        ("AA:7C:3F:10:00:01", "AA:7C:3F:10:00:08", 1.6, "ESP-NOW"),
        ("AA:7C:3F:10:00:01", "AA:7C:3F:10:00:04", 2.4, "ESP-NOW"),
        ("AA:7C:3F:10:00:08", "AA:7C:3F:10:00:06", 2.1, "ESP-NOW"),
        ("AA:7C:3F:10:00:03", "AA:7C:3F:10:00:06", 1.7, "ESP-NOW"),
        ("AA:7C:3F:10:00:04", "AA:7C:3F:10:00:06", 2.0, "ESP-NOW"),
        ("AA:7C:3F:10:00:08", "AA:7C:3F:20:00:11", 2.8, "ESP-NOW"),
        ("AA:7C:3F:10:00:05", "AA:7C:3F:10:00:07", 1.5, "Zigbee"),
        ("AA:7C:3F:10:00:05", "AA:7C:3F:20:00:11", 3.1, "ESP-NOW"),
        ("AA:7C:3F:10:00:07", "AA:7C:3F:20:00:11", 3.4, "ESP-NOW"),
        ("BB:91:44:20:10:04", "BB:91:44:20:10:01", 2.2, "Thread"),
        ("BB:91:44:20:10:01", "BB:91:44:20:10:02", 1.4, "Thread"),
        ("BB:91:44:20:10:01", "BB:91:44:20:10:03", 1.5, "Thread"),
        ("BB:91:44:20:10:04", "BB:91:44:20:10:03", 2.6, "Thread"),
        ("BB:91:44:20:10:04", "BB:91:44:20:10:05", 2.7, "Zigbee"),
        ("BB:91:44:20:10:05", "BB:91:44:20:10:06", 1.3, "Zigbee"),
        ("CC:18:90:30:00:21", "CC:18:90:30:00:23", 2.0, "ESP-NOW"),
        ("CC:18:90:30:00:21", "CC:18:90:30:00:24", 1.9, "ESP-NOW"),
        ("CC:18:90:30:00:21", "CC:18:90:30:00:22", 2.3, "ESP-NOW"),
        ("CC:18:90:30:00:22", "CC:18:90:30:00:25", 1.6, "ESP-NOW"),
        ("CC:18:90:30:00:23", "CC:18:90:30:00:24", 1.4, "ESP-NOW"),
        ("CC:18:90:30:00:24", "CC:18:90:30:00:26", 2.9, "Zigbee"),
        ("CC:18:90:30:00:21", "CC:18:90:30:00:26", 3.6, "ESP-NOW"),
        ("AA:7C:3F:20:00:11", "BB:91:44:20:10:04", 18.5, "ESP-NOW-LR"),
        ("BB:91:44:20:10:04", "CC:18:90:30:00:26", 21.0, "ESP-NOW-LR")
    ];
}
