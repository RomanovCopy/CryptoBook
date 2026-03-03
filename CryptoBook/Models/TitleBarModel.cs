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

        private readonly ILifetimeScope scope;
        private readonly IMainWindowViewModel mainWindowViewModel;

        private readonly IPageNavigationService pageNavigationService;

        /// <summary>
        /// высота TitleBar
        /// </summary>
        public double MyFontSize { get => height; set => SetProperty(ref height, value); }
        double height;





        /// <summary>
        /// текст внутри TitleBar
        /// </summary>
        public string MyText { get => myText; set => SetProperty(ref myText, value); }
        string myText;

        public TitleBarModel(IMainWindowViewModel mainWindowViewModel, IPageNavigationService pageNavigationService)
        {
            this.mainWindowViewModel = mainWindowViewModel ?? throw new ArgumentNullException(nameof(mainWindowViewModel));
            this.pageNavigationService = pageNavigationService ?? throw new ArgumentNullException(nameof(pageNavigationService));
        }

        public bool CanExecute_Loaded(object? obj)
        {
            return true;
        }
        public void Execute_Loaded(object? obj)
        {
            MyFontSize = Properties.Settings.Default.TitleBarMyFontSize;
            MyText = "Encrypto";
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
                scope.Resolve<MainWindow>().DragMove();
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

            return pageNavigationService.CanGoBack;
        }
        public void Execute_ButtonBack_Click(object? obj)
        {
            pageNavigationService.GoBack();
        }

        public bool CanExecute_ButtonForward_Click(object? obj)
        {
            return pageNavigationService.CanGoForward;
        }
        public void Execute_ButtonForward_Click(object? obj)
        {
            pageNavigationService.GoForward();
        }

        public bool CanExecute_ToggleMenu_Click(object? obj)
        {
            return true;
        }
        public void Execute_ToggleMenu_Click(object? obj)
        {
            mainWindowViewModel.ToggleMenuCommand.Execute(null);
        }

        public bool CanExecute_ButtonDarkThemeClick(object? obj)
        {
            return false;
        }
        public void Execute_ButtonDarkThemeClick(object? obj)
        {
        }


        public bool CanExecute_ButtonSettingsClick(object? obj)
        {
            return true;
        }
        public void Execute_ButtonSettingsClick(object? obj)
        {
        }

        public bool CanExecute_ButtonLightTheme_Click(object? obj)
        {
            return true;
        }
        public void Execute_ButtonLightTheme_Click(object? obj)
        {
        }


        public bool CanExecute_MinButtonClick(object? obj)
        {
            return true;
        }
        public void Execute_MinButtonClick(object? obj)
        {
            mainWindowViewModel.WindowState = System.Windows.WindowState.Minimized;
        }

        public bool CanExecute_MaxButtonClick(object? obj)
        {
            return mainWindowViewModel.WindowState != System.Windows.WindowState.Maximized;
        }
        public void Execute_MaxButtonClick(object? obj)
        {
            mainWindowViewModel.WindowState = System.Windows.WindowState.Maximized;
        }

        public bool CanExecute_CloseButtonClick(object? obj)
        {
            return mainWindowViewModel.Close.CanExecute(null);
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
            mainWindowViewModel.Close.Execute(null);
        }

        public bool CanExecute_GoToWindow(object? obj)
        {
            return mainWindowViewModel.WindowState != System.Windows.WindowState.Normal;
        }
        public void Execute_GoToWindow(object? obj)
        {
            mainWindowViewModel.WindowState = System.Windows.WindowState.Normal;
        }

        public bool CanExecute_Closing(object? obj)
        {
            return false;
        }
        public void Execute_Closing(object? obj)
        {
            Properties.Settings.Default.TitleBarMyFontSize = MyFontSize;
            Properties.Settings.Default.Save();
            mainWindowViewModel.Close.Execute(null);

        }

        public bool CanExecute_Closed(object? obj)
        {
            throw new NotImplementedException();
        }

        public void Execute_Closed(object? obj)
        {
            throw new NotImplementedException();
        }
    }
}
