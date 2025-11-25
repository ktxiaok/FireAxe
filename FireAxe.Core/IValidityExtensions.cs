using System;
using System.ComponentModel;

namespace FireAxe;

public static class IValidityExtensions
{
    public static IDisposable RegisterInvalidHandler<T>(this T obj, Action handler) where T : class, IValidity, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(obj);
        ArgumentNullException.ThrowIfNull(handler);

        if (!obj.IsValid)
        {
            handler();
            return DisposableUtils.Create(() => { });
        }

        IDisposable disposable = null!;
        PropertyChangedEventHandler propertyChangedHandler = (object? sender, PropertyChangedEventArgs args) =>
        {
            if (args.PropertyName == nameof(IValidity.IsValid))
            {
                if (!obj.IsValid)
                {
                    handler();
                    disposable.Dispose();
                }
            }
        };
        bool disposed = false;
        obj.PropertyChanged += propertyChangedHandler;
        disposable = DisposableUtils.Create(() =>
        {
            if (!disposed)
            {
                obj.PropertyChanged -= propertyChangedHandler;
                disposed = true;
            }
        });
        return disposable;
    }

    public static ValidRefCollection<T> ToValidRefCollection<T>(this IEnumerable<T> enumerable) where T : class, IValidity
    {
        ArgumentNullException.ThrowIfNull(enumerable);

        var collection = new ValidRefCollection<T>();
        foreach (var item in enumerable)
        {
            collection.Add(item);
        }
        return collection;
    }

    public static void ThrowIfInvalid(this IValidity obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        if (!obj.IsValid)
        {
            throw new InvalidOperationException($"The object {obj.GetType().Name} is invalid now.");
        }
    }
}