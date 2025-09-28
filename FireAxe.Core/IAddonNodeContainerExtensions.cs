using System;

namespace FireAxe;

public static class IAddonNodeContainerExtensions
{
    public static IEnumerable<AddonNode> GetDescendantNodes(this IAddonNodeContainer container) => container.GetDescendantsByDfsPreorder();

    public static void CheckDescendants(this IAddonNodeContainer container)
    {
        foreach (var node in container.GetDescendantsByDfsPostorder())
        {
            node.Check();
        }
    }
}
