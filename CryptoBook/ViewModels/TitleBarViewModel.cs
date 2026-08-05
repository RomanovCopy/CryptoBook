using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;

using System.ComponentModel;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class TitleBarViewModel: ViewModelBase, ITitleBarViewModel
    {

        private readonly ITitleBarModel titleBarModel;
        private readonly IDocumentTitleProvider documentTitleProvider;
        private readonly IPageNavigationService pageNavigationService;
        private bool disposed;

        public double MyFontSize => titleBarModel.MyFontSize;
        public string DocumentTitle => documentTitleProvider.Title;
        public string? DocumentPath => documentTitleProvider.Path;



        public TitleBarViewModel(
            ITitleBarModel titleBarModel,
            IDocumentTitleProvider documentTitleProvider,
            IPageNavigationService pageNavigationService)
        {
            this.titleBarModel = titleBarModel ??
                throw new ArgumentNullException(nameof(titleBarModel));
            this.documentTitleProvider = documentTitleProvider ??
                throw new ArgumentNullException(nameof(documentTitleProvider));
            this.pageNavigationService = pageNavigationService ??
                throw new ArgumentNullException(nameof(pageNavigationService));
            this.titleBarModel.PropertyChanged += OnTitleBarModelPropertyChanged;
            this.documentTitleProvider.PropertyChanged += OnDocumentTitlePropertyChanged;
            this.pageNavigationService.PropertyChanged +=
                OnPageNavigationServicePropertyChanged;
        }

        private void OnTitleBarModelPropertyChanged(
            object? sender,
            System.ComponentModel.PropertyChangedEventArgs args) =>
            OnPropertyChanged(args.PropertyName ?? string.Empty);

        private void OnDocumentTitlePropertyChanged(
            object? sender,
            PropertyChangedEventArgs args)
        {
            if(args.PropertyName == nameof(IDocumentTitleProvider.Title))
                OnPropertyChanged(nameof(DocumentTitle));
            else if(args.PropertyName == nameof(IDocumentTitleProvider.Path))
                OnPropertyChanged(nameof(DocumentPath));
        }

        private void OnPageNavigationServicePropertyChanged(
            object? sender,
            PropertyChangedEventArgs args)
        {
            if(args.PropertyName == nameof(IPageNavigationService.CanGoBack))
                buttonBack_Click?.RaiseCanExecuteChanged();
            else if(args.PropertyName ==
                    nameof(IPageNavigationService.CanGoForward))
                buttonForward_Click?.RaiseCanExecuteChanged();
        }

        public void Dispose()
        {
            if(disposed)
                return;

            disposed = true;
            titleBarModel.PropertyChanged -= OnTitleBarModelPropertyChanged;
            documentTitleProvider.PropertyChanged -= OnDocumentTitlePropertyChanged;
            pageNavigationService.PropertyChanged -=
                OnPageNavigationServicePropertyChanged;
        }




        public ICommand Loaded => loaded ??=
            new RelayCommand(titleBarModel.Execute_Loaded, titleBarModel.CanExecute_Loaded);
        RelayCommand loaded;

        public ICommand MouseLeftButtonDown =>
            mouseLeftButtonDown ??= new RelayCommand(titleBarModel.Execute_MouseLeftButtonDown, titleBarModel.CanExecute_MouseLeftButtonDown);
        RelayCommand mouseLeftButtonDown;

        public ICommand TitleBarDoubleClick => titleBarDoubleClick ??= new RelayCommand(titleBarModel.Execute_TitleBarDoubleClick, titleBarModel.CanExecute_TitleBarDoubleClick);
        RelayCommand titleBarDoubleClick;

        public ICommand TitleBarMouseMove => titleBarMouseMove ??= new RelayCommand(titleBarModel.Execute_TitleBarMouseMove, titleBarModel.CanExecute_TitleBarMouseMove);
        RelayCommand titleBarMouseMove;

        public ICommand ButtonBack_Click => buttonBack_Click ??= new RelayCommand(titleBarModel.Execute_ButtonBack_Click, titleBarModel.CanExecute_ButtonBack_Click);
        RelayCommand buttonBack_Click;

        public ICommand ButtonForward_Click => buttonForward_Click ??= new RelayCommand(titleBarModel.Execute_ButtonForward_Click, titleBarModel.CanExecute_ButtonForward_Click);
        RelayCommand buttonForward_Click;

        public ICommand ToggleMenu_Click => toggleMenu_Click ??= new RelayCommand(titleBarModel.Execute_ToggleMenu_Click, titleBarModel.CanExecute_ToggleMenu_Click);
        RelayCommand toggleMenu_Click;

        public ICommand ButtonSettingsClick => buttonSettingsClick ??= new RelayCommand(titleBarModel.Execute_ButtonSettingsClick, titleBarModel.CanExecute_ButtonSettingsClick);
        RelayCommand buttonSettingsClick;

        public ICommand MinButtonClick => minButtonClick ??= new RelayCommand(titleBarModel.Execute_MinButtonClick, titleBarModel.CanExecute_MinButtonClick);
        RelayCommand minButtonClick;

        public ICommand GoToWindow => goToWindow ??= new RelayCommand(titleBarModel.Execute_GoToWindow, titleBarModel.CanExecute_GoToWindow);
        RelayCommand goToWindow;

        public ICommand MaxButtonClick => maxButtonClick ??= new RelayCommand(titleBarModel.Execute_MaxButtonClick, titleBarModel.CanExecute_MaxButtonClick);
        RelayCommand maxButtonClick;

        public ICommand CloseButtonClick => closeButtonClick ??= new RelayCommand(titleBarModel.Execute_CloseButtonClick, titleBarModel.CanExecute_CloseButtonClick);
        RelayCommand closeButtonClick;

        public ICommand Close => close ??= new RelayCommand(titleBarModel.Execute_Close, titleBarModel.CanExecute_Close);
        RelayCommand close;

        public ICommand Closing => closing ??= new RelayCommand(titleBarModel.Execute_Closing, titleBarModel.CanExecute_Closing);
        RelayCommand closing;

        public ICommand Closed => closed ??=
            new RelayCommand(titleBarModel.Execute_Closed, titleBarModel.CanExecute_Closed);
        RelayCommand closed;
    }
}
