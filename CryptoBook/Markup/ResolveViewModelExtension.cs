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
    public class ResolveViewModelExtension: MarkupExtension
    {


        private static readonly ConcurrentDictionary<Type, object> viewModelCache = new();


        /// <summary>
        /// Тип ViewModel, который должен быть разрешен.
        /// </summary>
        public Type ViewModelType { get; set; }
        public object Parameter { get; set; }


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
                //var container = GetContainer();
                //object viewModel = container.Resolve(type)
                //    ?? throw new InvalidOperationException($"Type {type.FullName} could not be resolved from Autofac container.");


                //var target = serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;

                //var d = target?.TargetObject as DependencyObject;
                //if(d is null)
                //    return Binding.ReferenceEquals(target?.TargetObject, null) ? throw new InvalidOperationException("Target object is null.") : throw new InvalidOperationException($"Target object of type {target.TargetObject.GetType().FullName} is not a DependencyObject.");

                //var obj = target?.TargetObject ?? throw new InvalidOperationException("Target object is null.");


                var viewModel = AmbientScope.Current.Resolve(ViewModelType);


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


        private static Window? FindWindow(DependencyObject d)
        {
            for(DependencyObject? cur = d; cur is not null;)
            {
                if(cur is Window w)
                    return w;

                cur = VisualTreeHelper.GetParent(cur);
            }
            return null;
        }
    }
}
