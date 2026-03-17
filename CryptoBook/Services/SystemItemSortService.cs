using CryptoBook.Comparers;
using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Services
{
    public sealed class SystemItemSortService:ISystemItemSortService
    {
        public IComparer<ISystemItem> GetComparer( SystemItemSortType sortType, int direction)
        {
            int dir = direction >= 0 ? 1 : -1;

            return sortType switch
            {
                SystemItemSortType.Name =>
                    new SystemItemNameComparer(dir),

                SystemItemSortType.Type =>
                    new SystemItemExtensionComparer(dir),

                SystemItemSortType.Size =>
                    new SystemItemSizeComparer(dir),

                SystemItemSortType.Date =>
                    new SystemItemModifiedComparer(dir),

                _ => throw new ArgumentOutOfRangeException(nameof(sortType))
            };
        }

        public void Sort(IList<ISystemItem> items, SystemItemSortType sortType, int direction)
        {
            if(items == null)
                throw new ArgumentNullException(nameof(items));

            var comparer = GetComparer(sortType, direction);

            if(items is List<ISystemItem> list)
            {
                list.Sort(comparer);
                return;
            }

            var sorted = items
                .OrderBy(x => x, comparer)
                .ToArray();

            for(int i = 0; i < sorted.Length; i++)
                items[i] = sorted[i];
        }
    }
}
