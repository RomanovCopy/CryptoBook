using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IPropertyAccessor
    {
        T? Read<T>(object source, string name, T? fallback = default);
    }
}
