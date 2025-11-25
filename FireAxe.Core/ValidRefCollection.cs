using System;
using System.Collections;

namespace FireAxe;

public class ValidRefCollection<T> : IEnumerable<T> where T : class, IValidity
{
    private readonly List<ValidRef<T>> _list;

    public ValidRefCollection()
    {
        _list = new();
    }

    public void Add(T item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!item.IsValid)
        {
            return;
        }

        _list.Add(new ValidRef<T>(item));
    }

    public void Remove(T item)
    {
        ArgumentNullException.ThrowIfNull(item);

        for (int i = 0, len = _list.Count; i < len; i++)
        {
            if (EqualityComparer<T>.Default.Equals(_list[i].TryGet(), item))
            {
                _list.RemoveAt(i);
                return;
            }
        }
    }

    public void Clear() => _list.Clear();

    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < _list.Count;)
        {
            var item = _list[i].TryGet();
            if (item is null)
            {
                _list.RemoveAt(i);
            }
            else
            {
                yield return item;
                i++;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}