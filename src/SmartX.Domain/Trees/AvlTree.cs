namespace SmartX.Domain.Trees;

/// <summary>
/// Height-balanced BST. Range queries walk only the overlapping subtree,
/// so a moisture window for one MAC does not scan the whole ingest buffer.
/// </summary>
public sealed class AvlTree<TKey, TValue> where TKey : IComparable<TKey>
{
    private AvlNode? _root;

    public int Count { get; private set; }

    public void Insert(TKey key, TValue value)
    {
        _root = Insert(_root, key, value);
        Count++;
    }

    public IReadOnlyList<TValue> Range(TKey from, TKey to)
    {
        var hits = new List<TValue>();
        Collect(_root, from, to, hits);
        return hits;
    }

    private static AvlNode Insert(AvlNode? node, TKey key, TValue value)
    {
        if (node is null)
        {
            return new AvlNode(key, value);
        }

        var cmp = key.CompareTo(node.Key);
        if (cmp < 0)
        {
            node.Left = Insert(node.Left, key, value);
        }
        else
        {
            node.Right = Insert(node.Right, key, value);
        }

        return Balance(node);
    }

    private static void Collect(AvlNode? node, TKey from, TKey to, List<TValue> hits)
    {
        if (node is null)
        {
            return;
        }

        if (from.CompareTo(node.Key) < 0)
        {
            Collect(node.Left, from, to, hits);
        }

        if (from.CompareTo(node.Key) <= 0 && to.CompareTo(node.Key) >= 0)
        {
            hits.Add(node.Value);
        }

        if (to.CompareTo(node.Key) > 0)
        {
            Collect(node.Right, from, to, hits);
        }
    }

    private static AvlNode Balance(AvlNode node)
    {
        node.Refresh();
        var factor = node.Balance;
        if (factor > 1)
        {
            if (node.Left!.Balance < 0)
            {
                node.Left = RotateLeft(node.Left);
            }

            return RotateRight(node);
        }

        if (factor < -1)
        {
            if (node.Right!.Balance > 0)
            {
                node.Right = RotateRight(node.Right);
            }

            return RotateLeft(node);
        }

        return node;
    }

    private static AvlNode RotateLeft(AvlNode node)
    {
        var pivot = node.Right!;
        node.Right = pivot.Left;
        pivot.Left = node;
        node.Refresh();
        pivot.Refresh();
        return pivot;
    }

    private static AvlNode RotateRight(AvlNode node)
    {
        var pivot = node.Left!;
        node.Left = pivot.Right;
        pivot.Right = node;
        node.Refresh();
        pivot.Refresh();
        return pivot;
    }

    private sealed class AvlNode(TKey key, TValue value)
    {
        public TKey Key { get; } = key;
        public TValue Value { get; } = value;
        public AvlNode? Left { get; set; }
        public AvlNode? Right { get; set; }
        public int Height { get; private set; } = 1;

        public int Balance => HeightOf(Left) - HeightOf(Right);

        public void Refresh() => Height = 1 + Math.Max(HeightOf(Left), HeightOf(Right));

        private static int HeightOf(AvlNode? node) => node?.Height ?? 0;
    }
}

public readonly record struct TelemetryKey(DateTimeOffset Timestamp, Guid PacketId) : IComparable<TelemetryKey>
{
    public int CompareTo(TelemetryKey other)
    {
        var byTime = Timestamp.CompareTo(other.Timestamp);
        return byTime != 0 ? byTime : PacketId.CompareTo(other.PacketId);
    }

    public static TelemetryKey StartOf(DateTimeOffset timestamp) => new(timestamp, Guid.Empty);

    public static TelemetryKey EndOf(DateTimeOffset timestamp)
        => new(timestamp, Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"));
}
