using Autofac;

using CryptoBook.Injections;
using CryptoBook.Interfaces;

using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace CryptoBook.Markup
{
    /// <summary>
    /// MarkupExtension для разрешения ViewModel из контейнера Autofac с кэшированием.
    /// </summary>
    public class ResolveViewModelExtension: DiMarkupExtension
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
                var scope = GetScope(serviceProvider);
                var viewModel =  scope.Resolve(ViewModelType);
                if(viewModel is not IViewModel resolvedViewModel)
                    throw new InvalidOperationException($"Resolved type {type.FullName} does not implement IViewModel.");

                System.Diagnostics.Debug.WriteLine($"Added ViewModel for {type.FullName} to cache.");
                return resolvedViewModel;
            });
        }

    }
}
