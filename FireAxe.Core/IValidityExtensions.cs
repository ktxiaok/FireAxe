using System;

namespace FireAxe;

public static class IValidityExtensions
{
    public static ValidCollection<T> ToValidCollection<T>(this IEnumerable<T> enumerable) where T : class, IValidity
    {
        var collection = new ValidCollection<T>();
        foreach (var item in enumerable)
        {
            collection.Add(item);
        }
        return collection;
    }
}