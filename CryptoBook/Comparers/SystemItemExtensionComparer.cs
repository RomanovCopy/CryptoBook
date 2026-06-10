using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Comparers
{
    public sealed class SystemItemExtensionComparer: IComparer<ISystemItem>
    {
        private readonly int _dir;

        public SystemItemExtensionComparer(int dir)
        {
            _dir = dir >= 0 ? 1 : -1;
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

            if(x is IContainerSystemItem xc && y is IContainerSystemItem yc)
            {
                return string.Compare(
                    xc.Name,
                    yc.Name,
                    StringComparison.OrdinalIgnoreCase
                );
            }

            if(x is IFileItem xf && y is IFileItem yf)
            {
                int r = string.Compare(
                    xf.Extension,
                    yf.Extension,
                    StringComparison.OrdinalIgnoreCase
                );

                return r * _dir;
            }

            throw new InvalidOperationException(
                $"Unsupported item types: {x.GetType()} / {y.GetType()}"
            );
        }
    }
}
