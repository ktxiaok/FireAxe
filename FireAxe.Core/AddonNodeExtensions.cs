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

    public static void CheckSelfAndDescendants(this AddonNode addon)
    {
        ArgumentNullException.ThrowIfNull(addon);

        if (addon is IAddonNodeContainer container)
        {
            container.CheckDescendants();
        }
        addon.Check();
    }

    public static ValidTaskCreator<T> GetValidTaskCreator<T>(this T addon) where T : AddonNode
    {
        ArgumentNullException.ThrowIfNull(addon);

        return new ValidTaskCreator<T>(addon, new TaskFactory(addon.Root.TaskScheduler));
    }
}