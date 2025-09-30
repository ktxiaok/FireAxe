using System;
using ValveKeyValue;

namespace FireAxe;

internal static class KVObjectExtensions
{
    public static KVValue? TryGetChildValue(this KVObject obj, string name, StringComparison comparisonType)
    {
        ArgumentNullException.ThrowIfNull(name);

        foreach (KVObject child in obj.Children)
        {
            if (string.Equals(name, child.Name, comparisonType))
            {
                return child.Value;
            }
        }

        return null;
    }

    public static KVValue? TryGetChildValueIgnoreCase(this KVObject obj, string name)
    {
        return obj.TryGetChildValue(name, StringComparison.OrdinalIgnoreCase);
    }
}