using System;

namespace FireAxe
{
    public static class IAddonNodeContainerExtensions
    {
        public static IEnumerable<AddonNode> GetAllNodes(this IAddonNodeContainer container)
        {
            foreach (var node in container.GetDescendantsByDfsPreorder())
            {
                yield return node;
            }
        }

        public static void CheckDescendants(this IAddonNodeContainer container)
        {
            foreach (var node in container.GetDescendantsByDfsPostorder())
            {
                node.Check();
            }
        }
    }
}
