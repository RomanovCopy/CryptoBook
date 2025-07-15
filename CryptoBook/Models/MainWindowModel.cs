using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Views;

using System.Globalization;
using System.Windows;

namespace CryptoBook.Models
{
    public class MainWindowModel: ViewModelBase
    {
        private readonly IWindowManager windowManager;
        private bool isSideMenu { get; set; }

        internal double WindowWidth { get => windowWidth; set => SetProperty(ref windowWidth, value); }
        double windowWidth;
        internal double WindowHeight { get => windowHeight; set => SetProperty(ref windowHeight, value); }
        double windowHeight;
        internal double WindowTop { get => windowTop; set => SetProperty(ref windowTop, value); }
        double windowTop;
        internal double WindowLeft { get => windowLeft; set => SetProperty(ref windowLeft, value); }
        double windowLeft;
        internal WindowState WindowState { get => windowState; set => SetProperty(ref windowState, value); }
        WindowState windowState;



        internal static Action Ready { get; set; }
        public Guid WindowId { get; private set; }

        public MainWindowModel(IWindowManager windowManager)
        {
            this.windowManager = windowManager;
            WindowId = Guid.NewGuid();

            WindowHeight = Properties.Settings.Default.WindowHeight;
            WindowWidth = Properties.Settings.Default.WindowWidth;
            WindowLeft = Properties.Settings.Default.WindowLeft;
            WindowTop = Properties.Settings.Default.WindowTop;

            //восстанавливаем состояние окна
            WindowState = Properties.Settings.Default.WindowState == "Normal" ? WindowState.Normal : Properties.Settings.Default.WindowState == "Minimized" ? WindowState.Minimized : Properties.Settings.Default.WindowState == "Maximized" ? WindowState.Maximized :
                WindowState.Minimized;
        }





        internal bool CanExecute_windowToMinimize(object? obj)
        {
            return WindowState != WindowState.Minimized;
        }
        internal void Execute_windowToMinimize(object? obj)
        {
            WindowState = WindowState.Minimized;
        }

        internal bool CanExecute_WindowToMaximize(object? obj)
        {
            return WindowState != WindowState.Maximized;
        }
        internal void Execute_WindowToMaximize(object? obj)
        {
            WindowState = WindowState.Maximized;
        }

        internal bool CanExecute_WindowToNormal(object? obj)
        {
            return WindowState != WindowState.Normal;
        }
        internal void Execute_WindowToNormal(object? obj)
        {
            WindowState = WindowState.Normal;
        }


        internal bool CanExecute_Loaded(object? obj)
        {
            return true;
        }
        internal void Execute_Loaded(object? obj)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo(Properties.Settings.Default.CultureInfo);
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(Properties.Settings.Default.CultureInfo);

            if(Ready != null)
            {
                Ready.Invoke();
            }
        }


        internal bool CanExecute_Close(object? obj)
        {
            return true;
        }
        internal void Execute_Close(object? obj)
        {
            windowManager.CloseWindow<MainWindow>(WindowId);
        }



        internal bool CanExecute_Closing(object? obj)
        {
            return true;
        }
        internal void Execute_Closing(object? obj)
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
        internal bool CanExecute_Closed(object? obj)
        {
            return true;
        }

        internal void Execute_Closed(object? obj)
        {
            ((IDisposable)windowManager)?.Dispose();
        }

    }
}
