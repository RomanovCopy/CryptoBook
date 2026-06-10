using Autofac;

using CryptoBook.Interfaces;

using System.Collections.Concurrent;
using System.Windows.Markup;

namespace CryptoBook.Markup
{
    /// <summary>
    /// MarkupExtension для разрешения UserControl через DI-контейнер Autofac с кэшированием.
    /// </summary>
    public class ResolveUserControlExtension: MarkupExtension
    {
        /// <summary>
        /// Тип UserControl, который требуется разрешить через DI.
        /// </summary>
        public Type UserControlType { get; set; }

        private static readonly ConcurrentDictionary<Type, object> _cache = new();

        public ResolveUserControlExtension() { }

        public ResolveUserControlExtension(Type type)
        {
            UserControlType = type ?? throw new ArgumentNullException(nameof(type));
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if(UserControlType == null)
                throw new InvalidOperationException("UserControlType must be set.");

            return _cache.GetOrAdd(UserControlType, type =>
            {
                var container = GetContainer();
                return container.Resolve(type);
            });
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
