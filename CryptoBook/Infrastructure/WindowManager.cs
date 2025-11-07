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

        public T CreateWindow<T>() where T : Window
        {
            if(!_scope.IsRegistered<T>())
                throw new InvalidOperationException($"Window of type {typeof(T).Name} is not registered in the container.");
            var window = _scope.Resolve<T>();
            window.Owner = GetOwner();
            RegisterWindow<T>(window);
            return window;
        }


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
