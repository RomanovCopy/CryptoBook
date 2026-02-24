using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IWindowContext
    {
        IReadOnlyDictionary<string, object?> Items { get; }
        T Get<T>(string key);
        bool TryGet<T>(string key, out T value);
    }
}
