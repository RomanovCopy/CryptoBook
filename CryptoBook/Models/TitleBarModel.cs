using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Services;
using CryptoBook.ViewModels;
using CryptoBook.Views;

namespace CryptoBook.Models
{
    public class TitleBarModel: ViewModelBase, ITitleBarModel
    {
        /// <summary>
        /// окно перемещается
        /// </summary>
        private bool _isDragging;

        private readonly Guid _winId;

        private readonly IPageNavigationService _pageNavigationService;
        private readonly IWindowManager _windowManager;
        private readonly IMainWindowModel _mainWindowModel;
        private readonly ISettingsWindowService settingsWindowService;

        /// <summary>
        /// высота TitleBar
        /// </summary>
        public double MyFontSize { get => height; set => SetProperty(ref height, value); }
        double height;

        public TitleBarModel(
            IMainWindowModel mainWindowModel,
            IPageNavigationService pageNavigationService,
            IWindowManager windowManager,
            ISettingsWindowService settingsWindowService)
        {
            this._pageNavigationService = pageNavigationService ?? throw new ArgumentNullException(nameof(pageNavigationService));
            this._windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));  
            this._mainWindowModel = mainWindowModel ??
                throw new ArgumentNullException(nameof(mainWindowModel));
            this.settingsWindowService = settingsWindowService ??
                throw new ArgumentNullException(nameof(settingsWindowService));
            _winId = mainWindowModel.WindowId;
        }

        public bool CanExecute_Loaded(object? obj)
        {
            return true;
        }
        public void Execute_Loaded(object? obj)
        {
            MyFontSize = Properties.Settings.Default.TitleBarMyFontSize;
        }

        public bool CanExecute_TitleBarDoubleClick(object? obj)
        {
            return CanExecute_MaxButtonClick(null);
        }
        public void Execute_TitleBarDoubleClick(object? obj)
        {
            Execute_MaxButtonClick(null);
        }

        public bool CanExecute_MouseLeftButtonDown(object? obj)
        {
            return true;
        }
        public void Execute_MouseLeftButtonDown(object? obj)
        {

            if(!_isDragging)
            {
                _isDragging = true;
                _windowManager.FindHostWindow(_winId)?.Window.DragMove();
                _isDragging = false;
            }
        }

        public bool CanExecute_TitleBarMouseMove(object? obj)
        {
            return true;
        }
        public void Execute_TitleBarMouseMove(object? obj)
        {

        }

        public bool CanExecute_ButtonBack_Click(object? obj)
        {
            return _pageNavigationService.CanGoBack;
        }
        public void Execute_ButtonBack_Click(object? obj)
        {
            _pageNavigationService.GoBack();
        }

        public bool CanExecute_ButtonForward_Click(object? obj)
        {
            return _pageNavigationService.CanGoForward;
        }
        public void Execute_ButtonForward_Click(object? obj)
        {
            _pageNavigationService.GoForward();
        }

        public bool CanExecute_ToggleMenu_Click(object? obj)
        {
            return true;
        }
        public void Execute_ToggleMenu_Click(object? obj)
        {
            _mainWindowModel.Execute_ToggleMenuCommand(null);
        }

        public bool CanExecute_ButtonSettingsClick(object? obj)
        {
            return true;
        }
        public void Execute_ButtonSettingsClick(object? obj)
        {
            settingsWindowService.Open();
        }


        public bool CanExecute_MinButtonClick(object? obj)
        {
            return true;
        }
        public void Execute_MinButtonClick(object? obj)
        {
            _mainWindowModel.WindowState = System.Windows.WindowState.Minimized;
        }

        public bool CanExecute_MaxButtonClick(object? obj)
        {
            return _mainWindowModel.WindowState != System.Windows.WindowState.Maximized;
        }
        public void Execute_MaxButtonClick(object? obj)
        {
            _mainWindowModel.WindowState = System.Windows.WindowState.Maximized;
        }

        public bool CanExecute_CloseButtonClick(object? obj)
        {
            return _mainWindowModel.CanExecute_Close(null);
        }
        public void Execute_CloseButtonClick(object? obj)
        {
            Execute_Close(null);
        }

        public bool CanExecute_Close(object? obj)
        {
            return true;
        }
        public void Execute_Close(object? obj)
        {
            Properties.Settings.Default.TitleBarMyFontSize = MyFontSize;
            Properties.Settings.Default.Save();
            _mainWindowModel.Execute_Close(null);
        }

        public bool CanExecute_GoToWindow(object? obj)
        {
            return _mainWindowModel.WindowState != System.Windows.WindowState.Normal;
        }
        public void Execute_GoToWindow(object? obj)
        {
            _mainWindowModel.WindowState = System.Windows.WindowState.Normal;
        }

        public bool CanExecute_Closing(object? obj)
        {
            return false;
        }
        public void Execute_Closing(object? obj)
        {
            Properties.Settings.Default.TitleBarMyFontSize = MyFontSize;
            Properties.Settings.Default.Save();
            _mainWindowModel.Execute_Close(null);

        }

        public bool CanExecute_Closed(object? obj)
        {
            return true;
        }

        public void Execute_Closed(object? obj)
        {
        }
    }
}
