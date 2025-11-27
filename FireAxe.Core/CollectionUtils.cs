using System;
using System.Collections.Generic;

namespace FireAxe;

public static class CollectionUtils
{
    extension<T>(IEnumerable<T?>? source)
    {
        public IEnumerable<T> EliminateNull()
        {
            return (source?.Where(x => x is not null) ?? [])!;
        }
    }

    extension<T>(T?[]? array)
    {
        public T[] EliminateNull()
        {
            if (array is null)
            {
                return [];
            }
            bool hasNull = false;
            foreach (var item in array)
            {
                if (item is null)
                {
                    hasNull = true;
                    break;
                }
            }
            if (!hasNull)
            {
                return array!;
            }
            return ((IEnumerable<T?>)array).EliminateNull().ToArray();
        }
    }
}
