using System;
using System.Collections.Generic;
using System.Linq;

namespace FireAxe.ViewModels;

public class AddonNodeComparer : IComparer<AddonNode>
{
    public AddonNodeComparer(AddonNodeSortMethod sortMethod, bool isAscendingOrder)
    {
        SortMethod = sortMethod;
        IsAscendingOrder = isAscendingOrder;
    }

    public AddonNodeSortMethod SortMethod { get; }

    public bool IsAscendingOrder { get; }

    public int Compare(AddonNode? x, AddonNode? y)
    {
        if (x == null && y == null)
        {
            return 0;
        }
        if (x != null && y == null)
        {
            return 1;
        }
        if (x == null && y != null)
        {
            return -1;
        }

        int result = SortMethod switch
        {
            AddonNodeSortMethod.Name => x!.Name.CompareTo(y!.Name),
            AddonNodeSortMethod.EnableState => GetEnableState(x!).CompareTo(GetEnableState(y!)),
            AddonNodeSortMethod.FileSize => x!.FileSize.GetValueOrDefault(0).CompareTo(y!.FileSize.GetValueOrDefault(0)),
            AddonNodeSortMethod.CreationTime => x!.CreationTime.CompareTo(y!.CreationTime),
            AddonNodeSortMethod.Tag => CompareTags(x!, y!),
            _ => 0
        };

        if (!IsAscendingOrder)
        {
            result = -result;
        }

        return result;

        int GetEnableState(AddonNode node)
        {
            if (node.IsEnabled)
            {
                if (node.IsEnabledInHierarchy)
                {
                    return 0;
                }
                else
                {
                    return 1;
                }
            }
            else
            {
                return 2;
            }
        }

        int CompareTags(AddonNode x, AddonNode y)
        {
            var tags1 = x.TagsInHierarchy;
            var tags2 = y.TagsInHierarchy;
            int result = tags1.Count().CompareTo(tags2.Count());
            if (result != 0)
            {
                return result;
            }
            tags1 = tags1.Order();
            tags2 = tags2.Order();
            var tagEnumerator1 = tags1.GetEnumerator();
            var tagEnumerator2 = tags2.GetEnumerator();
            while (tagEnumerator1.MoveNext() && tagEnumerator2.MoveNext())
            {
                var tag1 = tagEnumerator1.Current;
                var tag2 = tagEnumerator2.Current;
                result = tag1.CompareTo(tag2);
                if (result != 0)
                {
                    return result;
                }
            }
            return 0;
        }
    }
}
