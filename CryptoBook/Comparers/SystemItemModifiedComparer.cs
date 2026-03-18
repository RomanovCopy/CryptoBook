using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Comparers
{
    public sealed class SystemItemModifiedComparer : IComparer<ISystemItem>
    {
        private readonly int _dir;

        public SystemItemModifiedComparer(int dir)
        {
            _dir = dir >= 0 ? 1 : -1;
        }

        public int Compare(ISystemItem? x, ISystemItem? y)
        {
            if (ReferenceEquals(x, y))
                return 0;
            if (x is null)
                return -1;
            if (y is null)
                return 1;

            // папки всегда выше файлов
            int d = SystemItemCompare.ContainerFirst(x, y);
            if (d != 0)
                return d;

            int result = x.LastWriteTimeUtc.CompareTo(y.LastWriteTimeUtc);
            // если даты совпали — сортируем по имени
            if(result == 0)
            {
                result = string.Compare(
                    x.Name,
                    y.Name,
                    StringComparison.OrdinalIgnoreCase
                );
            }

            return result * _dir;
        }
    }
}