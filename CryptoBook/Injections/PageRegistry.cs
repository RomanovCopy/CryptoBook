using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Injections
{
    public sealed class PageRegistry: IPageRegistry
    {
        private readonly IReadOnlyDictionary<string, Type> _map;

        public PageRegistry()
        {
            _map = new Dictionary<string, Type>
            {
                ["Home"] = typeof(MyPages.Home),
            };
        }

        public Type Resolve(string key)
        {
            if(!_map.TryGetValue(key, out var type))
                throw new KeyNotFoundException(
                    $"Page '{key}' is not registered.");

            return type;
        }
    }
}
