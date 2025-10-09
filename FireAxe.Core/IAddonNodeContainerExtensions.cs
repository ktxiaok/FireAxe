using System;

namespace FireAxe;

public static class IAddonNodeContainerExtensions
{
    public static void CheckDescendants(this IAddonNodeContainer container)
    {
        foreach (var node in container.GetDescendantsByDfsPostorder())
        {
            node.Check();
        }
    }
}
