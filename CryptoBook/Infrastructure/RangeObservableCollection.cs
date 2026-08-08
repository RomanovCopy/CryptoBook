using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace CryptoBook.Infrastructure;

/// <summary>
/// Наблюдаемая коллекция с одним Reset-уведомлением для массовой замены.
/// </summary>
public sealed class RangeObservableCollection<T>: ObservableCollection<T>
{
    public void ReplaceAll(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        Items.Clear();
        foreach(T item in items)
            Items.Add(item);

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(
            new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Reset));
    }
}
