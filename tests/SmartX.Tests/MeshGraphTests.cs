using SmartX.Domain.Mesh;
using Xunit;

namespace SmartX.Tests;

public class MeshGraphTests
{
    private static MeshGraph TinyMesh()
    {
        var graph = new MeshGraph();
        graph.AddVertex(new MeshVertex(MeshGraph.GatewayId, "GW", "Core", "Core", 0, 0, true));
        graph.AddVertex(new MeshVertex("A", "Node A", "Farm", "Z1", 1, 1, false));
        graph.AddVertex(new MeshVertex("B", "Node B", "Farm", "Z1", 2, 1, false));
        graph.AddVertex(new MeshVertex("C", "Node C", "Farm", "Z1", 3, 1, false));
        graph.AddUndirectedLink(MeshGraph.GatewayId, "A", 5, "ESP-NOW");
        graph.AddUndirectedLink("A", "B", 1.5, "ESP-NOW");
        graph.AddUndirectedLink("B", "C", 1.5, "ESP-NOW");
        graph.AddUndirectedLink("A", "C", 40, "ESP-NOW-LR");
        return graph;
    }

    [Fact]
    public void Kruskal_drops_the_expensive_shortcut()
    {
        var mst = TinyMesh().Kruskal(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        Assert.Equal("Kruskal", mst.Algorithm);
        Assert.Equal(3, mst.Edges.Count);
        Assert.DoesNotContain(mst.Edges, e => Math.Max(e.CostMilliJoule, 0) >= 40);
        Assert.Equal(8.0, mst.TotalMilliJoule);
    }

    [Fact]
    public void Bfs_skips_a_down_node_and_reports_partition()
    {
        var graph = new MeshGraph();
        graph.AddVertex(new MeshVertex(MeshGraph.GatewayId, "GW", "Core", "Core", 0, 0, true));
        graph.AddVertex(new MeshVertex("A", "Node A", "Farm", "Z1", 1, 1, false));
        graph.AddVertex(new MeshVertex("B", "Node B", "Farm", "Z1", 2, 1, false));
        graph.AddVertex(new MeshVertex("C", "Node C", "Farm", "Z1", 3, 1, false));
        graph.AddUndirectedLink(MeshGraph.GatewayId, "A", 5, "ESP-NOW");
        graph.AddUndirectedLink("A", "B", 1.5, "ESP-NOW");
        graph.AddUndirectedLink("B", "C", 1.5, "ESP-NOW");

        var down = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "B" };
        var walk = graph.BreadthFirst(MeshGraph.GatewayId, down);
        Assert.Contains("A", walk.Reachable);
        Assert.DoesNotContain("B", walk.Reachable);
        Assert.Contains("C", walk.Partitioned);
    }

    [Fact]
    public void Alternate_hops_name_live_neighbours_of_a_silent_node()
    {
        var down = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "B" };
        var hops = TinyMesh().AlternateHops("B", down);
        Assert.Contains(hops, h => h.To == "A");
        Assert.Contains(hops, h => h.To == "C");
    }
}
