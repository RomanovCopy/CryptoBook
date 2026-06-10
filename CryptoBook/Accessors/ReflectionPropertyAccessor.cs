using CryptoBook.Interfaces;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Accessors
{
    public sealed class ReflectionPropertyAccessor: IPropertyAccessor
    {

        private readonly ConcurrentDictionary<(Type, string), PropertyInfo?> _cache = new();


        public T? Read<T>(object source, string name, T? fallback = default)
        {
            if(source is null || string.IsNullOrWhiteSpace(name))
                return fallback;

            var type = source.GetType();
            var prop = _cache.GetOrAdd((type, name.ToLowerInvariant()),
                key => type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase));

            if(prop is null || !prop.CanRead)
                return fallback;

            var raw = prop.GetValue(source);
            if(raw is null)
                return fallback;
            if(raw is T t)
                return t;

            try
            {
                var target = typeof(T);
                var underlying = Nullable.GetUnderlyingType(target) ?? target;

                if(underlying.IsEnum)
                {
                    if(raw is string s)
                        return (T)Enum.Parse(underlying, s, true);
                    var num = Convert.ChangeType(raw, Enum.GetUnderlyingType(underlying), CultureInfo.InvariantCulture);
                    return (T)Enum.ToObject(underlying, num!);
                }

                if(raw is IConvertible && typeof(IConvertible).IsAssignableFrom(underlying))
                {
                    var converted = Convert.ChangeType(raw, underlying, CultureInfo.InvariantCulture);
                    return (T)converted!;
                }

                return (T)raw;
            } catch
            {
                return fallback;
            }
        }
    }
}
