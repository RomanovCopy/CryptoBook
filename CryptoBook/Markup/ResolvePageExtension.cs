using Autofac;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;

using CryptoBook.Interfaces;

namespace CryptoBook.Markup
{
    /// <summary>
    /// MarkupExtension для разрешения Page через DI-контейнер Autofac с кэшированием.
    /// </summary>
    public class ResolvePageExtension:MarkupExtension
    {
        /// <summary>
        /// Тип Page, который требуется разрешить через DI.
        /// </summary>
        public Type? PageType { get; set; }

        private static readonly ConcurrentDictionary<Type, object> _cache = new();

        public ResolvePageExtension() { }

        public ResolvePageExtension(Type type)
        {
           PageType = type ?? throw new ArgumentNullException(nameof(type));
        }


        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if(PageType == null)
                throw new InvalidOperationException("PageType must be set.");

            return _cache.GetOrAdd(PageType, type =>
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
