using System;
using System.Collections.Generic;

namespace FireAxe.ViewModels
{
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
        }
    }
}
