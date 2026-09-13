namespace SmartX.Domain.Deployment;

public enum DeploymentKind
{
    Facility = 0,
    Zone = 1,
    SubZone = 2,
    Node = 3
}

public sealed class DeploymentNode
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required DeploymentKind Kind { get; init; }
    public string? MacAddress { get; init; }
    public List<DeploymentNode> Children { get; init; } = [];
}

public sealed record DeploymentValidationResult(bool IsSafe, IReadOnlyList<string> Errors, string ResolvedPath);

/// <summary>
/// Recursively validates that a node is safely nested, e.g.
/// <c>Sub-Zone B → Zone 1 → Facility A</c>.
/// </summary>
public static class DeploymentValidator
{
    private static readonly DeploymentKind[] ExpectedOrder =
    [
        DeploymentKind.Facility,
        DeploymentKind.Zone,
        DeploymentKind.SubZone,
        DeploymentKind.Node
    ];

    public static DeploymentValidationResult Validate(DeploymentNode root, string? requiredLeafMac = null)
    {
        var errors = new List<string>();
        ValidateRecursive(root, depth: 0, ancestralPath: [], errors, requiredLeafMac);
        var path = DescribePath(root);
        return new DeploymentValidationResult(errors.Count == 0, errors, path);
    }

    private static void ValidateRecursive(
        DeploymentNode node,
        int depth,
        List<string> ancestralPath,
        List<string> errors,
        string? requiredLeafMac)
    {
        if (depth >= ExpectedOrder.Length)
        {
            errors.Add($"'{node.Name}' exceeds the four-tier Facility → Zone → Sub-Zone → Node tree.");
            return;
        }

        var expected = ExpectedOrder[depth];
        if (node.Kind != expected)
        {
            errors.Add($"'{node.Name}' is {node.Kind} but must be {expected} at depth {depth}.");
        }

        if (string.IsNullOrWhiteSpace(node.Name))
        {
            errors.Add($"A {expected} node is missing a name.");
        }

        if (node.Kind == DeploymentKind.Node)
        {
            if (string.IsNullOrWhiteSpace(node.MacAddress))
            {
                errors.Add($"Leaf node '{node.Name}' has no MAC address / unique identifier.");
            }
            else if (requiredLeafMac is not null
                     && !node.MacAddress.Equals(requiredLeafMac, StringComparison.OrdinalIgnoreCase)
                     && !ContainsMac(node, requiredLeafMac))
            {
                // Leaf itself may be a sibling container-less node; MAC check is exact on this leaf.
            }

            if (node.Children.Count > 0)
            {
                errors.Add($"Leaf node '{node.Name}' cannot host child deployments.");
            }
        }

        var nextPath = new List<string>(ancestralPath) { $"{node.Kind}:{node.Name}" };
        foreach (var child in node.Children)
        {
            ValidateRecursive(child, depth + 1, nextPath, errors, requiredLeafMac);
        }

        if (requiredLeafMac is not null && depth == 0 && !ContainsMac(node, requiredLeafMac))
        {
            errors.Add($"MAC {requiredLeafMac} is not present in this deployment tree.");
        }
    }

    private static bool ContainsMac(DeploymentNode node, string mac)
    {
        if (node.MacAddress is not null
            && node.MacAddress.Equals(mac, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var child in node.Children)
        {
            if (ContainsMac(child, mac))
            {
                return true;
            }
        }

        return false;
    }

    public static string DescribePath(DeploymentNode node)
    {
        if (node.Children.Count == 0)
        {
            return node.Name;
        }

        var childPaths = node.Children.Select(DescribePath);
        return $"{node.Name} → {string.Join(" | ", childPaths)}";
    }

    /// <summary>
    /// Builds the ancestor chain for a specific MAC so the UI can show
    /// "Node is safely configured within Sub-Zone B → Zone 1 → Facility A".
    /// </summary>
    public static IReadOnlyList<string>? FindAncestorChain(DeploymentNode node, string mac)
    {
        return Walk(node, mac, []);
    }

    private static List<string>? Walk(DeploymentNode node, string mac, List<string> trail)
    {
        var next = new List<string>(trail) { node.Name };
        if (node.MacAddress is not null
            && node.MacAddress.Equals(mac, StringComparison.OrdinalIgnoreCase))
        {
            next.Reverse();
            return next;
        }

        foreach (var child in node.Children)
        {
            var hit = Walk(child, mac, next);
            if (hit is not null)
            {
                return hit;
            }
        }

        return null;
    }
}
