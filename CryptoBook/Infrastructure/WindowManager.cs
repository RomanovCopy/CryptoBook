using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using Autofac;

using CryptoBook.Injections;
using CryptoBook.Interfaces;
using CryptoBook.MyControls;
using CryptoBook.ViewModels;
using CryptoBook.Views;

namespace CryptoBook.Infrastructure
{
    public class WindowManager: IWindowManager, IDisposable
    {
        private readonly ILifetimeScope _scope;
        private readonly HashSet<Window> _openWindows;

        public WindowManager(ILifetimeScope scope)
        {
            _scope = scope;
            _openWindows = [];
        }

        public T CreateWindow<T>() where T : Window
        {
            if(!_scope.IsRegistered<T>())
                throw new InvalidOperationException($"Window of type {typeof(T).Name} is not registered in the container.");
            var window = _scope.Resolve<T>();
            //if(typeof(T) == typeof(MainWindow))
            //{
            //    window.PreviewMouseLeftButtonDown += MainWindow_PreviewMouseLeftButtonDown;
            //    window.Closed += (s,e) => window.PreviewMouseLeftButtonDown -= MainWindow_PreviewMouseLeftButtonDown;
            //}
            RegisterWindow<T>(window);
            return window;
        }

        //private void MainWindow_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        //{
        //    var sideMenu = _scope.Resolve < SideMenu > ();
        //    var point = e.GetPosition(sideMenu);
        //    var window = _scope.Resolve<MainWindow>();
        //    var viewmodel = (MainWindowViewModel)window.DataContext;
        //    if(viewmodel.IsMenuOpen && point.X > sideMenu.ActualWidth)
        //    {
        //        viewmodel.IsMenuOpen = false;
        //    }
        //    e.Handled = false;
        //}

        public void ShowWindow<T>(Guid windowId) where T : Window
        {
            var window = FindWindow<T>(windowId);
            window?.Show();
        }

        public void CloseWindow<T>(Guid windowId) where T : Window
        {
            if(IsWindowOpen<T>(windowId))
            {
                var window = FindWindow<T>(windowId);
                if(window != null)
                {
                    window.Close();
                    UnregisterWindow<T>(window);
                }
            }
        }

        public bool IsWindowOpen<T>(Guid windowId) where T : Window
        {
            return FindWindow<T>(windowId) != null;
        }

        public T? FindWindow<T>(Guid windowId) where T : Window
        {
            return _openWindows
                .OfType<T>()
                .FirstOrDefault(w => w.DataContext is { } dc && dc is IWindowWithId id && id.WindowId == windowId);
        }



        private void RegisterWindow<T>(T window) where T : Window
        {

            if(window.DataContext is ICloseable vm)
            {
                vm.RequestClose += (s, e) => WinClose<T>(window);
            }
            _openWindows.Add(window);
            window.Closed += (s, e) => UnregisterWindow<T>(window);
        }

        private void UnregisterWindow<T>(T window) where T : Window
        {
            if(window.DataContext is ICloseable vm)
            {
                vm.RequestClose -= (s, e) => WinClose<T>(window);
            }
            window.Closed -= (s, e) => UnregisterWindow<T>(window);
            _openWindows.Remove(window);
        }

        private void WinClose<T>(T? window) where T : Window
        {
            window?.Close();
        }

        public void Dispose()
        {
            _scope?.Dispose();
        }
    }

}
