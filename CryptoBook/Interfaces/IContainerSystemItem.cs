using CryptoBook.DTO;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IContainerSystemItem:ISystemItem
    {
        string Name { get; set; }
        bool IsLoaded { get; set; }
        bool IsExpanded { get; set; }
        ReadOnlyObservableCollection<ISystemItem> Children { get; }
        FileOperationResult AddChild(ISystemItem item);
        FileOperationResult RemoveChild(ISystemItem item);
        FileOperationResult ClearChildren();
        /// <summary>
        /// Синхронизирует коллекцию дочерних элементов текущего контейнера с указанной коллекцией <paramref name="source"/>.
        /// Для каждого элемента <paramref name="source"/> вычисляется ключ с помощью <paramref name="keySelector"/>, по которому
        /// производится сопоставление с существующими дочерними элементами. В результате должны быть:
        /// добавлены новые элементы для отсутствующих ключей, обновлены или оставлены существующие элементы для совпадающих ключей,
        /// и удалены элементы, ключи которых отсутствуют в <paramref name="source"/>.
        /// </summary>
        /// <typeparam name="T">Тип элементов исходной коллекции <paramref name="source"/>.</typeparam>
        /// <typeparam name="TKey">Тип ключа, используемого для сопоставления элементов.</typeparam>
        /// <param name="source">Коллекция источника, с которой требуется синхронизировать дочерние элементы.</param>
        /// <param name="keySelector">Функция, возвращающая ключ для элемента <paramref name="source"/>; используется для поиска соответствия с существующими дочерними элементами.</param>
        /// <remarks>
        /// Конкретная стратегия создания и обновления дочерних элементов определяется реализацией интерфейса.
        /// Реализация должна учитывать потокобезопасность и корректную генерацию событий уведомления об изменениях
        /// (например, для привязки в UI).
        /// </remarks>
        void SyncCollections<T, TKey>( IEnumerable<T> source, Func<T, TKey> keySelector);
    }
}
