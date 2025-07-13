using CryptoBook.Interfaces;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;

namespace CryptoBook.Markup
{
    public class ResolveUserControlExtension:MarkupExtension
    {

        private readonly Type _type;
        // Статический кэш для хранения экземпляров UserControl
        private static readonly ConcurrentDictionary<Type, object> _cache = new ConcurrentDictionary<Type, object>();

        public ResolveUserControlExtension(Type type)
        {
            _type = type ?? throw new ArgumentNullException(nameof(type));
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            // Проверяем, есть ли экземпляр в кэше
            if(_cache.TryGetValue(_type, out var cachedInstance))
            {
                return cachedInstance;
            }

            // Получение контейнера Autofac
            var container = GetContainer();

            // Разрешение зависимости через Autofac
            var instance = container.Resolve(_type);

            // Сохраняем экземпляр в кэше
            _cache.TryAdd(_type, instance);

            return instance;
        }

        private static IContainer GetContainer()
        {
            if(System.Windows.Application.Current is not IContainerProvider containerProvider ||
                containerProvider.Container is not IContainer container)
            {
                throw new InvalidOperationException("Autofac container not found in Application.Current. " +
                    "Ensure your Application implements IContainerProvider and Container is properly initialized.");
            }
            return container;
        }
    }
}
