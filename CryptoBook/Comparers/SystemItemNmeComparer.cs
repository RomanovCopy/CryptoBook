using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Comparers
{
    public sealed class SystemItemNmeComparer:IComparer<ISystemItem>
    {
        private readonly int _dir;

        public SystemItemNmeComparer(int dir)
        {
            _dir = dir > 0 ? 1 : -1;
        }

        public int Compare(ISystemItem? x, ISystemItem? y)
        {
            if(ReferenceEquals(x, y))
                return 0;
            if(x is null)
                return -1;
            if(y is null)
                return 1;

            int d = SystemItemCompare.ContainerFirst(x, y);
            if(d != 0)
                return d;

            int r = string.Compare(
                x.Name,
                y.Name,
                StringComparison.OrdinalIgnoreCase
            );

            return r * _dir;
        }
    }
}
