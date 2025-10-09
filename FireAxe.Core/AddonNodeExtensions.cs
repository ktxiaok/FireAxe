using System;

namespace FireAxe;

public static class AddonNodeExtensions
{
    public static IEnumerable<AddonNode> GetAllNodesEnabledInHierarchy(this AddonNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        using var enumerator = node.GetSelfAndDescendantsEnumeratorByDfsPreorder();
        while (enumerator.MoveNext())
        {
            var current = enumerator.Current;
            if (current.IsEnabledInHierarchy)
            {
                yield return current;
            }
            else
            {
                enumerator.SkipDescendantsOfCurrent();
            }
        }
    }
}