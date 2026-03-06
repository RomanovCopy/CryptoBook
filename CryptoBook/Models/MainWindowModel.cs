using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Injections;
using CryptoBook.Interfaces;
using CryptoBook.Views;

using System.Globalization;
using System.Windows;

namespace CryptoBook.Models
{
    public class MainWindowModel: ViewModelBase ,IMainWindowModel
    {
        private readonly IWindowManager windowManager;
        public bool IsMenuOpen { get => isMenuOpen; set => SetProperty(ref isMenuOpen, value); }
        bool isMenuOpen;

        public double WindowWidth { get => windowWidth; set => SetProperty(ref windowWidth, value); }
        double windowWidth;
        public double WindowHeight { get => windowHeight; set => SetProperty(ref windowHeight, value); }
        double windowHeight;
        public double WindowTop { get => windowTop; set => SetProperty(ref windowTop, value); }
        double windowTop;
        public double WindowLeft { get => windowLeft; set => SetProperty(ref windowLeft, value); }
        double windowLeft;
        public WindowState WindowState { get => windowState; set => SetProperty(ref windowState, value); }
        WindowState windowState;



        public static Action Ready { get; set; }
        public Guid WindowId { get; private set; }

        public MainWindowModel(IWindowManager windowManager)
        {
            this.windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
            WindowId = Guid.NewGuid();

            WindowHeight = Properties.Settings.Default.WindowHeight;
            WindowWidth = Properties.Settings.Default.WindowWidth;
            WindowLeft = Properties.Settings.Default.WindowLeft;
            WindowTop = Properties.Settings.Default.WindowTop;

            //восстанавливаем состояние окна
            WindowState = Properties.Settings.Default.WindowState == "Normal" ? WindowState.Normal : Properties.Settings.Default.WindowState == "Minimized" ? WindowState.Minimized : Properties.Settings.Default.WindowState == "Maximized" ? WindowState.Maximized :
                WindowState.Minimized;
            IsMenuOpen = false;

        }

        public bool CanExecute_WindowToMinimize(object? obj)
        {
            return WindowState != WindowState.Minimized;
        }
        public void Execute_WindowToMinimize(object? obj)
        {
            WindowState = WindowState.Minimized;
        }

        public bool CanExecute_WindowToMaximize(object? obj)
        {
            return WindowState != WindowState.Maximized;
        }
        public void Execute_WindowToMaximize(object? obj)
        {
            WindowState = WindowState.Maximized;
        }

        public bool CanExecute_WindowToNormal(object? obj)
        {
            return WindowState != WindowState.Normal;
        }
        public void Execute_WindowToNormal(object? obj)
        {
            WindowState = WindowState.Normal;
        }

        public bool CanExecute_ToggleMenuCommand(object? obj)
        {
            return true;
        }
        public void Execute_ToggleMenuCommand(object? obj)
        {
            IsMenuOpen = !IsMenuOpen;
        }

        public bool CanExecute_SideMenuClose(object? obj)
        {
            return IsMenuOpen;
        }
        public void Execute_SideMenuClose(object? obj)
        {
            IsMenuOpen = false;
        }

        public bool CanExecute_Loaded(object? obj)
        {
            return true;
        }
        public void Execute_Loaded(object? obj)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo(Properties.Settings.Default.CultureInfo);
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(Properties.Settings.Default.CultureInfo);

            if(Ready != null)
            {
                Ready.Invoke();
            }
        }

        public bool CanExecute_Close(object? obj)
        {
            return true;
        }
        public void Execute_Close(object? obj)
        {
            windowManager.CloseWindow(WindowId);
        }

        public bool CanExecute_Closing(object? obj)
        {
            return true;
        }
        public void Execute_Closing(object? obj)
        {
            try
            {
                //размеры и положение окна
                if(WindowState.ToString() == "Normal")
                {
                    Properties.Settings.Default.WindowWidth = WindowWidth;
                    Properties.Settings.Default.WindowHeight = WindowHeight;
                    Properties.Settings.Default.WindowLeft = WindowLeft;
                    Properties.Settings.Default.WindowTop = WindowTop;
                }
                Properties.Settings.Default.WindowState = WindowState.ToString();
                Properties.Settings.Default.Save();
            } catch(Exception e) { ErrorWindow(e); }

        }

        public bool CanExecute_Closed(object? obj)
        {
            return true;
        }
        public void Execute_Closed(object? obj)
        {
            ((IDisposable)windowManager)?.Dispose();
        }

    }
}
