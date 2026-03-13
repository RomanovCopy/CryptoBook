using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Comparers
{
    public static class SystemItemCompare
    {
        public static int ContainerFirst(ISystemItem x, ISystemItem y)
        {
            bool xc = x is IContainerSystemItem;
            bool yc = y is IContainerSystemItem;

            if(xc == yc)
                return 0;

            return xc ? -1 : 1;
        }
    }
}
