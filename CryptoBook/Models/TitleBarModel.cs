using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.ViewModels;
using CryptoBook.Views;



namespace CryptoBook.Models
{
    internal class TitleBarModel: ViewModelBase
    {
        /// <summary>
        /// окно перемещается
        /// </summary>
        private bool _isDragging;

        private readonly ILifetimeScope scope;
        private readonly MyFrameViewModel myFrameViewModel;
        private readonly MainWindowViewModel mainWindowViewModel;

        /// <summary>
        /// высота TitleBar
        /// </summary>
        internal double MyFontSize { get => height; set => SetProperty(ref height, value); }
        double height;





        /// <summary>
        /// текст внутри TitleBar
        /// </summary>
        internal string MyText { get => myText; set => SetProperty(ref myText, value); }
        string myText;

        internal TitleBarModel(ILifetimeScope _scope)
        {
            scope = _scope;
            myFrameViewModel = (MyFrameViewModel)scope.Resolve<IMyFrameViewModel>();
            mainWindowViewModel = (MainWindowViewModel)scope.Resolve<IMainWindowViewModel>();
        }

        internal bool CanExecute_Loaded(object? obj)
        {
            return true;
        }
        internal void Execute_Loaded(object? obj)
        {
            MyFontSize = Properties.Settings.Default.TitleBarMyFontSize;
            MyText = "Encrypto";
        }

        internal bool CanExecute_TitleBarDoubleClick(object? obj)
        {
            return CanExecute_MaxButtonClick(null);
        }
        internal void Execute_TitleBarDoubleClick(object? obj)
        {
            Execute_MaxButtonClick(null);
        }

        internal bool CanExecute_MouseLeftButtonDown(object? obj)
        {
            return true;
        }
        internal void Execute_MouseLeftButtonDown(object? obj)
        {

            if(!_isDragging)
            {
                _isDragging = true;
                ((IContainerProvider)System.Windows.Application.Current).Container.Resolve<MainWindow>().DragMove();
                _isDragging = false;
            }
        }

        internal bool CanExecute_TitleBarMouseMove(object? obj)
        {
            return true;
        }
        internal void Execute_TitleBarMouseMove(object? obj)
        {

        }

        internal bool CanExecute_ButtonBack_Click(object? obj)
        {

            return myFrameViewModel.FramelistGoBack.CanExecute(null);
        }
        internal void Execute_ButtonBack_Click(object? obj)
        {
            myFrameViewModel.FramelistGoBack.Execute(null);
        }

        internal bool CanExecute_ButtonForward_Click(object? obj)
        {
            return myFrameViewModel.FramelistGoForward.CanExecute(null);
        }
        internal void Execute_ButtonForward_Click(object? obj)
        {
            myFrameViewModel.FramelistGoForward.Execute(null);
        }

        internal bool CanExecute_ToggleMenu_Click(object? obj)
        {
            return true;
        }
        internal void Execute_ToggleMenu_Click(object? obj)
        {
            mainWindowViewModel.ToggleMenuCommand.Execute(null);
        }

        internal bool CanExecute_ButtonDarkThemeClick(object? obj)
        {
            return false;
        }
        internal void Execute_ButtonDarkThemeClick(object? obj)
        {
        }


        internal bool CanExecute_ButtonSettingsClick(object? obj)
        {
            return true;
        }
        internal void Execute_ButtonSettingsClick(object? obj)
        {
        }

        internal bool CanExecute_ButtonLightTheme_Click(object? obj)
        {
            return true;
        }
        internal void Execute_ButtonLightTheme_Click(object? obj)
        {
        }


        internal bool CanExecute_MinButtonClick(object? obj)
        {
            return true;
        }
        internal void Execute_MinButtonClick(object? obj)
        {
            mainWindowViewModel.WindowState = System.Windows.WindowState.Minimized;
        }

        internal bool CanExecute_MaxButtonClick(object? obj)
        {
            return mainWindowViewModel.WindowState != System.Windows.WindowState.Maximized;
        }
        internal void Execute_MaxButtonClick(object? obj)
        {
            mainWindowViewModel.WindowState = System.Windows.WindowState.Maximized;
        }

        internal bool CanExecute_CloseButtonClick(object? obj)
        {
            return mainWindowViewModel.Close.CanExecute(null);
        }
        internal void Execute_CloseButtonClick(object? obj)
        {
            Execute_Close(null);
        }

        internal bool CanExecute_Close(object? obj)
        {
            return true;
        }
        internal void Execute_Close(object? obj)
        {
            Properties.Settings.Default.TitleBarMyFontSize = MyFontSize;
            Properties.Settings.Default.Save();
            mainWindowViewModel.Close.Execute(null);
        }

        internal bool CanExecute_GoToWindow(object? obj)
        {
            return mainWindowViewModel.WindowState != System.Windows.WindowState.Normal;
        }
        internal void Execute_GoToWindow(object? obj)
        {
            mainWindowViewModel.WindowState = System.Windows.WindowState.Normal;
        }

        internal bool CanExecute_Closing(object? obj)
        {
            return false;
        }
        internal void Execute_Closing(object? obj)
        {
            Properties.Settings.Default.TitleBarMyFontSize = MyFontSize;
            Properties.Settings.Default.Save();
            mainWindowViewModel.Close.Execute(null);

        }

    }
}
