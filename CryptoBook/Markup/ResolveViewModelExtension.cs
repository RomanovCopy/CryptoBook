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
    /// MarkupExtension для разрешения ViewModel из контейнера Autofac.
    /// </summary>
    public class ResolveViewModelExtension: DiMarkupExtension
    {
        public Type ViewModelType { get; set; }

        public override object? ProvideValue(IServiceProvider serviceProvider)
        {
            if(ViewModelType == null)
                throw new InvalidOperationException("ViewModelType must be specified.");

            if(!typeof(IViewModel).IsAssignableFrom(ViewModelType))
                throw new InvalidOperationException($"Type {ViewModelType.FullName} must implement IViewModel.");

            var scope = GetScope(serviceProvider)?? throw new InvalidOperationException("Unable to retrieve Autofac scope from service provider.");
            var viewModel = scope.Resolve(ViewModelType)?? throw new InvalidOperationException($"Unable to resolve ViewModel of type {ViewModelType.FullName}.");
            return viewModel;

        }

    }
}
