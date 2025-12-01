using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace FireAxe;

/// <summary>
/// Once an element becomes invalid, it will be removed automatically.
/// After disposal, this collection will dispose all the subscriptions of elements and become read-only.
/// </summary>
public sealed class ObservableValidRefCollection<T>
    : IList<T>, IReadOnlyList<T>, INotifyPropertyChanged, INotifyCollectionChanged, IDisposable
    where T : class, IValidity, INotifyPropertyChanged
{
    private bool _disposed = false;

    private bool _notifyingCollectionChanged = false;

    private readonly ObservableCollectionAdvanced<T> _collection = new();

    private readonly Dictionary<T, IDisposable> _subscriptions = new();

    public ObservableValidRefCollection()
    {
        ((INotifyPropertyChanged)_collection).PropertyChanged += (sender, args) =>
        {
            PropertyChanged?.Invoke(this, args);
        };
        _collection.CollectionChanged += (sender, args) =>
        {
            _notifyingCollectionChanged = true;
            try
            {
                CollectionChanged?.Invoke(this, args);
            }
            finally
            {
                _notifyingCollectionChanged = false;
            }
        };
    }

    public T this[int index]
    {
        get
        {
            return _collection[index];
        }
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            ThrowIfDisposed();
            ThrowIfCannotModify();

            var oldItem = _collection[index];
            if (EqualityComparer<T>.Default.Equals(value, oldItem))
            {
                return;
            }

            if (value.IsValid)
            {
                SubscribeIfNot(value);
                _collection[index] = value;
            }
            else
            {
                _collection.RemoveAt(index);
            }
            UnsubscribeIfAbsent(oldItem);
        }
    }

    public int Count
    {
        get
        {
            return _collection.Count;
        }
    }

    bool ICollection<T>.IsReadOnly => false;

    public event PropertyChangedEventHandler? PropertyChanged = null;

    public event NotifyCollectionChangedEventHandler? CollectionChanged = null;

    public ReadOnlyObservableCollection<T> AsReadOnlyObservableCollection() => new ReadOnlyObservableCollection<T>(_collection);

    public void Add(T item)
    {
        ArgumentNullException.ThrowIfNull(item);
        ThrowIfDisposed();
        ThrowIfCannotModify();

        if (!item.IsValid)
        {
            return;
        }

        SubscribeIfNot(item);
        _collection.Add(item);
    }

    public void Clear()
    {
        ThrowIfDisposed();
        ThrowIfCannotModify();

        UnsubscribeAll();
        _collection.Clear();
    }

    public void Reset(IEnumerable<T> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);
        ThrowIfDisposed();
        ThrowIfCannotModify();

        UnsubscribeAll();
        var elementArray = elements.ToArray();
        foreach (var element in elementArray)
        {
            SubscribeIfNot(element);
        }
        _collection.Reset(elementArray);
    }

    public bool Contains(T item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (_disposed)
        {
            return _collection.Contains(item);
        }

        return _subscriptions.ContainsKey(item);
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        _collection.CopyTo(array, arrayIndex);
    }

    public IEnumerator<T> GetEnumerator()
    {
        return _collection.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public int IndexOf(T item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return _collection.IndexOf(item);
    }

    public void Insert(int index, T item)
    {
        ArgumentNullException.ThrowIfNull(item);
        ThrowIfDisposed();
        ThrowIfCannotModify();

        if (!item.IsValid)
        {
            return;
        }

        SubscribeIfNot(item);
        _collection.Insert(index, item);
    }

    public bool Remove(T item)
    {
        ArgumentNullException.ThrowIfNull(item);
        ThrowIfDisposed();
        ThrowIfCannotModify();

        if (!_subscriptions.ContainsKey(item))
        {
            return false;
        }

        _collection.Remove(item);
        UnsubscribeIfAbsent(item);

        return true;
    }

    public void RemoveAt(int index)
    {
        ThrowIfDisposed();
        ThrowIfCannotModify();

        var item = _collection[index];
        _collection.RemoveAt(index);
        UnsubscribeIfAbsent(item);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        UnsubscribeAll();
        _disposed = true;
    }

    private void SubscribeIfNot(T item)
    {
        if (_subscriptions.ContainsKey(item))
        {
            return;
        }

        IDisposable subscription = null!;
        subscription = item.RegisterInvalidHandler(() =>
        {
            subscription.Dispose();
            _subscriptions.Remove(item);
            for (int i = 0; i < _collection.Count;)
            {
                if (EqualityComparer<T>.Default.Equals(_collection[i], item))
                {
                    _collection.RemoveAt(i);
                }
                else
                {
                    i++;
                }
            }
        });
        _subscriptions[item] = subscription;
    }

    private void UnsubscribeIfAbsent(T item)
    {
        foreach (var item0 in _collection)
        {
            if (EqualityComparer<T>.Default.Equals(item0, item))
            {
                return;
            }
        }

        _subscriptions[item].Dispose();
        _subscriptions.Remove(item);
    }

    private void UnsubscribeAll()
    {
        foreach (var subscription in _subscriptions.Values)
        {
            subscription.Dispose();
        }
        _subscriptions.Clear();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }
    }

    private void ThrowIfCannotModify()
    {
        if (_notifyingCollectionChanged)
        {
            if (CollectionChanged is not null && CollectionChanged.GetInvocationList().Length > 1)
            {
                throw new InvalidOperationException($"Cannot modify the collection during notifying multiple {nameof(NotifyCollectionChangedEventHandler)}.");
            }
        }
    }
}