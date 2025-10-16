using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace FireAxe;

internal class ObservableCollectionAdvanced<T> : ObservableCollection<T>
{
    private bool _suppressNotifications = false;

    public void Reset(IEnumerable<T> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);
        CheckReentrancy();

        _suppressNotifications = true;
        try
        {
            Clear();
            foreach (var element in elements)
            {
                Add(element);
            }
        }
        finally
        {
            _suppressNotifications = false;
        }

        NotifyCountChanged();
        NotifyIndexerChanged();
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (_suppressNotifications)
        {
            return;
        }

        base.OnCollectionChanged(e);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (_suppressNotifications)
        {
            return;
        }

        base.OnPropertyChanged(e);
    }

    private void NotifyCountChanged() => OnPropertyChanged(new PropertyChangedEventArgs("Count"));

    private void NotifyIndexerChanged() => OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
}