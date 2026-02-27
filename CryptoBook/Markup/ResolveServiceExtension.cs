using Autofac;

using CryptoBook.Injections;
using CryptoBook.Interfaces;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;

namespace CryptoBook.Markup
{
    public class ResolveServiceExtension:DiMarkupExtension
    {
            /// <summary>
            /// Тип Service, который требуется разрешить через DI.
            /// </summary>
        public Type ServiceType { get; set; }

        private static readonly ConcurrentDictionary<Type, object> _cache = new();

        public ResolveServiceExtension() { }

        public ResolveServiceExtension(Type type)
        {
            ServiceType = type ?? throw new ArgumentNullException(nameof(type));
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if(ServiceType == null)
                throw new InvalidOperationException("UserControlType must be set.");
            if(!typeof(IService).IsAssignableFrom(ServiceType))
                throw new InvalidOperationException($"Type {ServiceType.FullName} must implement IViewModel.");

            return _cache.GetOrAdd(ServiceType, type =>
            {
                var scope = GetScope(serviceProvider);
                object service = scope.Resolve(type)
                    ?? throw new InvalidOperationException($"Type {type.FullName} could not be resolved from Autofac container.");

                if(service is not IService resolvedService)
                    throw new InvalidOperationException($"Resolved type {type.FullName} does not implement IViewModel.");

                System.Diagnostics.Debug.WriteLine($"Added ViewModel for {type.FullName} to cache.");
                return resolvedService;
            });
        }

        //private static IContainer GetContainer()
        //{
        //    if(System.Windows.Application.Current is not IContainerProvider containerProvider ||
        //        containerProvider.Container is not IContainer container)
        //    {
        //        throw new InvalidOperationException("Autofac container not found in Application.Current. " +
        //            "Ensure your Application implements IContainerProvider and Container is properly initialized.");
        //    }
        //    return container;
        //}
    }
}
