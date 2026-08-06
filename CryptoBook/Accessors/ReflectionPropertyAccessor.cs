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
    /// <summary>
    /// Предоставляет универсальный доступ к публичным свойствам объектов с помощью рефлексии.
    /// </summary>
    /// <remarks>
    /// Найденные свойства кэшируются, а полученные значения при необходимости преобразуются
    /// к запрошенному типу. Если свойство отсутствует, недоступно для чтения или значение
    /// невозможно преобразовать, возвращается указанное значение по умолчанию.
    /// </remarks>
    public sealed class ReflectionPropertyAccessor : IPropertyAccessor
    {
        // Ключ включает фактический тип объекта и нормализованное имя свойства.
        // ConcurrentDictionary позволяет безопасно переиспользовать экземпляр класса в разных потоках.
        private readonly ConcurrentDictionary<(Type, string), PropertyInfo?> _cache = new();

        /// <summary>
        /// Читает значение публичного свойства объекта без учёта регистра его имени.
        /// </summary>
        /// <typeparam name="T">Тип, в котором требуется получить значение свойства.</typeparam>
        /// <param name="source">Объект, содержащий требуемое свойство.</param>
        /// <param name="name">Имя свойства.</param>
        /// <param name="fallback">
        /// Значение, возвращаемое при некорректных аргументах, отсутствии свойства,
        /// значении <see langword="null"/> или ошибке преобразования.
        /// </param>
        /// <returns>Значение свойства, приведённое к типу <typeparamref name="T"/>, либо <paramref name="fallback"/>.</returns>
        public T? Read<T>(object source, string name, T? fallback = default)
        {
            if (source is null || string.IsNullOrWhiteSpace(name))
                return fallback;

            var type = source.GetType();

            // Сохраняем в кэше также отрицательный результат поиска, чтобы не повторять рефлексию.
            var prop = _cache.GetOrAdd((type, name.ToLowerInvariant()),
                key => type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase));

            if (prop is null || !prop.CanRead)
                return fallback;

            var raw = prop.GetValue(source);
            if (raw is null)
                return fallback;
            if (raw is T t)
                return t;

            try
            {
                var target = typeof(T);

                // Для Nullable<T> преобразование выполняется к его базовому типу.
                var underlying = Nullable.GetUnderlyingType(target) ?? target;

                if (underlying.IsEnum)
                {
                    // Перечисления поддерживают как текстовые имена, так и числовые значения.
                    if (raw is string s)
                        return (T)Enum.Parse(underlying, s, true);
                    var num = Convert.ChangeType(raw, Enum.GetUnderlyingType(underlying), CultureInfo.InvariantCulture);
                    return (T)Enum.ToObject(underlying, num!);
                }

                // Инвариантная культура исключает зависимость результата от региональных настроек процесса.
                if (raw is IConvertible && typeof(IConvertible).IsAssignableFrom(underlying))
                {
                    var converted = Convert.ChangeType(raw, underlying, CultureInfo.InvariantCulture);
                    return (T)converted!;
                }

                return (T)raw;
            }
            catch
            {
                // Контракт метода предполагает безопасный fallback вместо передачи ошибок преобразования наружу.
                return fallback;
            }
        }
    }
}
