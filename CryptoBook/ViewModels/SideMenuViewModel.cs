using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;
using DTO = CryptoBook.DTO;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class SideMenuViewModel: ViewModelBase, ISideMenuViewModel, IDisposable
    {

        private readonly SideMenuModel sideMenuModel;
        private readonly IDocumentTitleProvider documentTitleProvider;
        private bool disposed;

        public string BookTitle => documentTitleProvider.Title;
        public IPinnedDocumentsViewModel PinnedDocuments { get; }
        public ObservableCollection<MenuItemBase> MenuItems { get => sideMenuModel.MenuItems; }
        public ObservableCollection<DTO.MenuItem> QuickActions { get => sideMenuModel.QuickActions; }
        public double Width { get => sideMenuModel.Width; set => sideMenuModel.Width = value; }

        public double FontSizeHeader { get => sideMenuModel.FontSizeHeader; set => sideMenuModel.FontSizeHeader = value; }
        public double FontSize { get => sideMenuModel.FontSize; set => sideMenuModel.FontSize = value; }

        public SideMenuViewModel(ILifetimeScope scope)
        {
            sideMenuModel = new(scope);
            documentTitleProvider = scope.Resolve<IDocumentTitleProvider>();
            PinnedDocuments = scope.Resolve<IPinnedDocumentsViewModel>();
            sideMenuModel.PropertyChanged += OnModelPropertyChanged;
            documentTitleProvider.PropertyChanged += OnDocumentTitleChanged;
        }

        private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs args) =>
            OnPropertyChanged(args.PropertyName ?? string.Empty);

        private void OnDocumentTitleChanged(object? sender, PropertyChangedEventArgs args)
        {
            if(args.PropertyName == nameof(IDocumentTitleProvider.Title))
                OnPropertyChanged(nameof(BookTitle));
        }

        public ICommand Loaded => loaded ??=
            new AsyncRelayCommand(ExecuteLoadedAsync, sideMenuModel.CanExecute_Lifecycle);
        AsyncRelayCommand loaded;

        private async Task ExecuteLoadedAsync(
            object? parameter,
            CancellationToken cancellationToken)
        {
            sideMenuModel.Execute_Loaded(parameter);
            await PinnedDocuments.InitializeAsync(cancellationToken);
        }

        public ICommand Close => close ??=
            new RelayCommand(sideMenuModel.Execute_Close, sideMenuModel.CanExecute_Lifecycle);
        RelayCommand close;

        public ICommand Closing => closing ??=
            new RelayCommand(sideMenuModel.Execute_Closing, sideMenuModel.CanExecute_Lifecycle);
        RelayCommand closing;

        public ICommand Closed => closed ??=
            new RelayCommand(ExecuteClosed, sideMenuModel.CanExecute_Lifecycle);
        RelayCommand closed;

        private void ExecuteClosed(object? parameter) => Dispose();

        public void Dispose()
        {
            if(disposed)
                return;
            disposed = true;
            loaded?.Cancel();
            sideMenuModel.PropertyChanged -= OnModelPropertyChanged;
            documentTitleProvider.PropertyChanged -= OnDocumentTitleChanged;
            PinnedDocuments.Dispose();
            sideMenuModel.Dispose();
        }
    }
}
