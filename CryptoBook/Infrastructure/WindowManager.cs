using Autofac;

using CryptoBook.Styles;
using CryptoBook.Interfaces;
using CryptoBook.Views;

using System.Windows;

namespace CryptoBook.Infrastructure
{
    public class WindowManager: IWindowManager, IDisposable
    {
        private readonly ILifetimeScope _scope;
        private readonly HashSet<Window> _openWindows;

        static Window? GetOwner()
        {
            var windows = System.Windows.Application.Current.Windows;
            if(windows.Count > 0)
            {
                foreach(Window vin in windows)
                {
                    if(vin.IsActive)
                        return vin;
                }
            }
            return null;
        }

        public WindowManager(ILifetimeScope scope)
        {
            _scope = scope;
            _openWindows = [];
        }

        public Guid CreateWindow<T>() where T : Window
        {
            if(!_scope.IsRegistered<T>())
                throw new InvalidOperationException($"Window of type {typeof(T).Name} is not registered in the container.");
            var window = _scope.Resolve<T>();
            window.Owner = GetOwner();
            RegisterWindow<T>(window);
            if(window.DataContext is not IWindowWithId)
                throw new InvalidOperationException($"The DataContext of window type {typeof(T).Name} does not implement IWindowWithId.");
            return ((IWindowWithId)window.DataContext).WindowId;
        }


        public void ShowWindow(Guid windowId)
        {
            var window = FindWindow(windowId);
            if(window is Window win)
                win.Show();
        }

        public void CloseWindow(Guid windowId)
        {
            if(IsWindowOpen(windowId))
            {
                var window = FindWindow(windowId);
                if(window is Window win)
                {
                    WinClose(win);
                    UnregisterWindow(win);
                }
            }
        }

        public bool IsWindowOpen(Guid windowId)
        {
            return FindWindow(windowId) != null;
        }

        public object? FindWindow(Guid windowId)
        {
            return _openWindows.FirstOrDefault(w => w.DataContext is { } dc && dc is IWindowWithId id && id.WindowId == windowId);
        }

        public IEnumerable<T> FindWindow<T>() where T : Window
        {
            return _openWindows.OfType<T>();
        }



        private void RegisterWindow<T>(T window) where T : Window
        {
            if(window.DataContext is ICloseable vm)
            {
                vm.RequestClose += (s, e) => WinClose<T>(window);
            }
            window.Closed += (s, e) => UnregisterWindow<T>(window);
            _openWindows.Add(window);
        }

        private void UnregisterWindow<T>(T window) where T : Window
        {
            if(window.DataContext is ICloseable vm)
            {
                vm.RequestClose -= (s, e) => WinClose<T>(window);
            }
            window.Closed -= (s, e) => UnregisterWindow<T>(window);
            (window.Parent as Window)?.Focus();
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
