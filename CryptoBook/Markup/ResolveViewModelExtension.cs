using Autofac;

using CryptoBook.Interfaces;

using System.Collections.Concurrent;
using System.Windows.Markup;

namespace CryptoBook.Markup
{
    /// <summary>
    /// MarkupExtension для разрешения ViewModel из контейнера Autofac с кэшированием.
    /// </summary>
    public class ResolveViewModelExtension: MarkupExtension
    {


        private static readonly ConcurrentDictionary<Type, object> viewModelCache = new();


        /// <summary>
        /// Тип ViewModel, который должен быть разрешен.
        /// </summary>
        public Type ViewModelType { get; set; }



        /// <summary>
        /// Сбросить кэш ViewModel (например, для тестирования).
        /// </summary>
        public static void ClearCache() => viewModelCache.Clear();


        public override object? ProvideValue(IServiceProvider serviceProvider)
        {
            if(ViewModelType == null)
                throw new InvalidOperationException("ViewModelType must be specified.");

            if(!typeof(IViewModel).IsAssignableFrom(ViewModelType))
                throw new InvalidOperationException($"Type {ViewModelType.FullName} must implement IViewModel.");

            return viewModelCache.GetOrAdd(ViewModelType, type =>
            {
                var container = GetContainer();
                object viewModel = container.Resolve(type)
                    ?? throw new InvalidOperationException($"Type {type.FullName} could not be resolved from Autofac container.");

                if(viewModel is not IViewModel resolvedViewModel)
                    throw new InvalidOperationException($"Resolved type {type.FullName} does not implement IViewModel.");

                System.Diagnostics.Debug.WriteLine($"Added ViewModel for {type.FullName} to cache.");
                return resolvedViewModel;
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
