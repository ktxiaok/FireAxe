using System;

namespace L4D2AddonAssistant
{
    public static class IAddonNodeContainerExtensions
    {
        public static IEnumerable<AddonNode> GetAllNodes(this IAddonNodeContainer container)
        {
            foreach (var node1 in container.Nodes)
            {
                foreach (var node2 in node1.GetSelfAndDescendantsByDfsPreorder())
                {
                    yield return node2;
                }
            }
        }
    }
}
