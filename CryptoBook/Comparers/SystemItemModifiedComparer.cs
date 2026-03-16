using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Comparers
{
    public sealed class SystemItemModifiedComparer : IComparer<SystemItem>
    {
        private readonly int _dir;

        public SystemItemModifiedComparer(int dir)
        {
            _dir = dir >= 0 ? 1 : -1;
        }

        int IComparer<SystemItem>.Compare(SystemItem? x, SystemItem? y)
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

            // если оба каталога — сортируем по имени
            if (x is IContainerSystemItem xc && y is IContainerSystemItem yc)
            {
                int r = string.Compare(
                    xc.Name,
                    yc.Name,
                    StringComparison.OrdinalIgnoreCase
                );
                return r * _dir;
            }

            // оба файла — сортируем по дате изменения
            int result = x.LastWriteTimeUtc.CompareTo(y.LastWriteTimeUtc);

            if (result == 0)
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