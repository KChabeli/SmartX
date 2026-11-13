namespace SmartX.Domain.Mesh;

public sealed record MeshVertex(
    string Id,
    string Label,
    string Facility,
    string Zone,
    double X,
    double Y,
    bool IsGateway);

public sealed record MeshLink(
    string From,
    string To,
    double CostMilliJoule,
    string Radio);

public sealed record MeshHop(string From, string To, double CostMilliJoule);

public sealed class BfsWalk
{
    public required string Origin { get; init; }
    public required IReadOnlyList<string> VisitOrder { get; init; }
    public required IReadOnlyDictionary<string, string?> Parent { get; init; }
    public required IReadOnlyList<string> Reachable { get; init; }
    public required IReadOnlyList<string> Partitioned { get; init; }
}

public sealed class MstResult
{
    public required IReadOnlyList<MeshLink> Edges { get; init; }
    public required double TotalMilliJoule { get; init; }
    public required string Algorithm { get; init; }
    public required IReadOnlyDictionary<string, string?> ParentFromGateway { get; init; }
}

/// <summary>
/// Undirected weighted mesh. Vertices are ESP32 nodes plus the Smart-X gateway.
/// Edge cost is a milli-joule radio budget so Kruskal can pick the cheapest backbone.
/// </summary>
public sealed class MeshGraph
{
    private readonly Dictionary<string, MeshVertex> _vertices = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<MeshLink> _links = [];
    private readonly Dictionary<string, List<MeshLink>> _adjacency = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<MeshVertex> Vertices => _vertices.Values;
    public IReadOnlyList<MeshLink> Links => _links;
    public const string GatewayId = "GW-SMARTX";

    public void AddVertex(MeshVertex vertex)
    {
        _vertices[vertex.Id] = vertex;
        if (!_adjacency.ContainsKey(vertex.Id))
        {
            _adjacency[vertex.Id] = [];
        }
    }

    public void AddUndirectedLink(string from, string to, double costMilliJoule, string radio)
    {
        if (!_vertices.ContainsKey(from) || !_vertices.ContainsKey(to))
        {
            throw new InvalidOperationException($"Cannot link unknown vertices {from} → {to}.");
        }

        var link = new MeshLink(from, to, costMilliJoule, radio);
        _links.Add(link);
        _adjacency[from].Add(link);
        _adjacency[to].Add(new MeshLink(to, from, costMilliJoule, radio));
    }

    public MeshVertex Get(string id) => _vertices[id];

    public IReadOnlyList<MeshLink> Neighbours(string id)
        => _adjacency.TryGetValue(id, out var list) ? list : [];

    public BfsWalk BreadthFirst(string origin, IReadOnlySet<string> down)
    {
        var visit = new List<string>();
        var parent = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();

        if (!_vertices.ContainsKey(origin) || down.Contains(origin))
        {
            return new BfsWalk
            {
                Origin = origin,
                VisitOrder = visit,
                Parent = parent,
                Reachable = visit,
                Partitioned = _vertices.Keys.Where(id => !id.Equals(origin, StringComparison.OrdinalIgnoreCase)).ToList()
            };
        }

        queue.Enqueue(origin);
        seen.Add(origin);
        parent[origin] = null;

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            visit.Add(current);
            foreach (var link in Neighbours(current))
            {
                if (down.Contains(link.To) || !seen.Add(link.To))
                {
                    continue;
                }

                parent[link.To] = current;
                queue.Enqueue(link.To);
            }
        }

        var partitioned = _vertices.Keys
            .Where(id => !seen.Contains(id))
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new BfsWalk
        {
            Origin = origin,
            VisitOrder = visit,
            Parent = parent,
            Reachable = visit,
            Partitioned = partitioned
        };
    }

    public IReadOnlyList<string> PathFromParents(IReadOnlyDictionary<string, string?> parent, string target)
    {
        if (!parent.ContainsKey(target))
        {
            return [];
        }

        var hops = new List<string>();
        string? cursor = target;
        var guard = 0;
        while (cursor is not null && guard++ < _vertices.Count + 2)
        {
            hops.Add(cursor);
            parent.TryGetValue(cursor, out cursor);
        }

        hops.Reverse();
        return hops;
    }

    public IReadOnlyList<MeshHop> AlternateHops(string downMac, IReadOnlySet<string> down)
    {
        return Neighbours(downMac)
            .Where(link => !down.Contains(link.To))
            .OrderBy(link => link.CostMilliJoule)
            .Select(link => new MeshHop(downMac, link.To, link.CostMilliJoule))
            .ToList();
    }

    /// <summary>
    /// Kruskal MST on live vertices. Cheapest radio edges that still span the mesh.
    /// </summary>
    public MstResult Kruskal(IReadOnlySet<string> down)
    {
        var live = _vertices.Keys
            .Where(id => !down.Contains(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var forest = new UnionFind(live);
        var chosen = new List<MeshLink>();
        var total = 0d;

        foreach (var link in _links.OrderBy(l => l.CostMilliJoule).ThenBy(l => l.From).ThenBy(l => l.To))
        {
            if (!live.Contains(link.From) || !live.Contains(link.To))
            {
                continue;
            }

            if (!forest.Union(link.From, link.To))
            {
                continue;
            }

            chosen.Add(link);
            total += link.CostMilliJoule;
        }

        var mstGraph = new MeshGraph();
        foreach (var id in live)
        {
            mstGraph.AddVertex(_vertices[id]);
        }

        foreach (var edge in chosen)
        {
            mstGraph.AddUndirectedLink(edge.From, edge.To, edge.CostMilliJoule, edge.Radio);
        }

        var walk = mstGraph.BreadthFirst(GatewayId, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        return new MstResult
        {
            Edges = chosen,
            TotalMilliJoule = Math.Round(total, 2),
            Algorithm = "Kruskal",
            ParentFromGateway = walk.Parent
        };
    }
}

internal sealed class UnionFind
{
    private readonly Dictionary<string, string> _parent;
    private readonly Dictionary<string, int> _rank;

    public UnionFind(IEnumerable<string> ids)
    {
        _parent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _rank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in ids)
        {
            _parent[id] = id;
            _rank[id] = 0;
        }
    }

    public bool Union(string a, string b)
    {
        var rootA = Find(a);
        var rootB = Find(b);
        if (rootA.Equals(rootB, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (_rank[rootA] < _rank[rootB])
        {
            _parent[rootA] = rootB;
        }
        else if (_rank[rootA] > _rank[rootB])
        {
            _parent[rootB] = rootA;
        }
        else
        {
            _parent[rootB] = rootA;
            _rank[rootA]++;
        }

        return true;
    }

    private string Find(string id)
    {
        if (!_parent[id].Equals(id, StringComparison.OrdinalIgnoreCase))
        {
            _parent[id] = Find(_parent[id]);
        }

        return _parent[id];
    }
}
