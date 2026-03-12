using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.DTO
{
    public class WindowContext : IWindowContext
    {
        private readonly IReadOnlyDictionary<string, object?> _items;
        public WindowContext(IReadOnlyDictionary<string, object?> items) => _items = items;
        public IReadOnlyDictionary<string, object?> Items => _items;

        public T Get<T>(string key) => _items.TryGetValue(key, out var v) && v is T t ? t
                : throw new KeyNotFoundException($"Key '{key}' not found or has different type.");

        public bool TryGet<T>(string key, out T value)
        {
            if(_items.TryGetValue(key, out var v) && v is T t)
            { value = t; return true; }
            value = default!;
            return false;
        }
    }
}
