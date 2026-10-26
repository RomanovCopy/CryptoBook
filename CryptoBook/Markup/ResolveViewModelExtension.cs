using Autofac;

using CryptoBook.Injections;
using System.ComponentModel;
using System.Windows.Markup;

namespace CryptoBook.Markup
{
    /// <summary>
    /// MarkupExtension для разрешения ViewModel из контейнера Autofac.
    /// Поддерживает модели панелей без команд жизненного цикла окна из IViewModel.
    /// </summary>
    public class ResolveViewModelExtension: DiMarkupExtension
    {
        public Type ViewModelType { get; set; }

        public override object? ProvideValue(IServiceProvider serviceProvider)
        {
            if(ViewModelType == null)
                throw new InvalidOperationException("ViewModelType must be specified.");

            if(!typeof(INotifyPropertyChanged).IsAssignableFrom(ViewModelType))
                throw new InvalidOperationException($"Type {ViewModelType.FullName} must implement INotifyPropertyChanged.");

            var scope = GetScope(serviceProvider)?? throw new InvalidOperationException("Unable to retrieve Autofac scope from service provider.");
            var viewModel = scope.Resolve(ViewModelType)?? throw new InvalidOperationException($"Unable to resolve ViewModel of type {ViewModelType.FullName}.");
            return viewModel;

        }

    }
}
