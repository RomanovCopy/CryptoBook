using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Infrastructure
{
    public sealed class FilteredReadOnlyObservableCollection<TBase, TFiltered>: IDisposable
    where TFiltered : class, TBase
    {
        private readonly ObservableCollection<TBase> _source;
        private readonly ObservableCollection<TFiltered> _filteredMutable;
        private readonly ReadOnlyObservableCollection<TFiltered> _filteredReadOnly;

        public ReadOnlyObservableCollection<TFiltered> View => _filteredReadOnly;

        public FilteredReadOnlyObservableCollection(ObservableCollection<TBase> source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));

            _filteredMutable = new ObservableCollection<TFiltered>(_source.OfType<TFiltered>());
            _filteredReadOnly = new ReadOnlyObservableCollection<TFiltered>(_filteredMutable);

            _source.CollectionChanged += OnSourceCollectionChanged;
        }

        private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // ВАЖНО: считаем, что _source и _filteredMutable живут на одном UI-потоке (WPF).
            switch(e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                AddItems(e.NewItems);
                break;

                case NotifyCollectionChangedAction.Remove:
                RemoveItems(e.OldItems);
                break;

                case NotifyCollectionChangedAction.Replace:
                RemoveItems(e.OldItems);
                AddItems(e.NewItems);
                break;

                case NotifyCollectionChangedAction.Move:
                // Если вам критичен порядок, можно реализовать Move, но обычно для фильтра
                // проще пересобрать по source.
                Rebuild();
                break;

                case NotifyCollectionChangedAction.Reset:
                Rebuild();
                break;
            }
        }

        private void AddItems(System.Collections.IList? newItems)
        {
            if(newItems is null)
                return;

            foreach(var obj in newItems)
            {
                if(obj is TFiltered item)
                {
                    // Вставим в filtered так, чтобы порядок соответствовал source.
                    var insertIndex = GetFilteredInsertIndex(item);
                    _filteredMutable.Insert(insertIndex, item);
                }
            }
        }

        private void RemoveItems(System.Collections.IList? oldItems)
        {
            if(oldItems is null)
                return;

            foreach(var obj in oldItems)
            {
                if(obj is TFiltered item)
                {
                    _filteredMutable.Remove(item);
                }
            }
        }

        private int GetFilteredInsertIndex(TFiltered newItem)
        {
            // Индекс в source
            var sourceIndex = _source.IndexOf(newItem);
            if(sourceIndex <= 0)
                return 0;

            // Считаем сколько TFiltered элементов находится в source до sourceIndex
            var countBefore = 0;
            for(int i = 0; i < sourceIndex; i++)
            {
                if(_source[i] is TFiltered)
                    countBefore++;
            }
            return countBefore;
        }

        private void Rebuild()
        {
            _filteredMutable.Clear();
            foreach(var item in _source.OfType<TFiltered>())
                _filteredMutable.Add(item);
        }

        public void Dispose()
        {
            _source.CollectionChanged -= OnSourceCollectionChanged;
        }
    }
}
