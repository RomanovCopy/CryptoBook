using Autofac;

using CryptoBook.Styles;
using CryptoBook.Interfaces;
using CryptoBook.Views;

using System.Windows;
using CryptoBook.DTO;
using CryptoBook.Injections;

namespace CryptoBook.Infrastructure
{
    public class WindowManager: IWindowManager, IDisposable
    {
        private readonly ILifetimeScope _root;
        private readonly Dictionary<Guid, WindowHost> _windowHosts;

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
            _root = scope;
            _windowHosts = [];
        }

        public Guid CreateWindow<T>( IReadOnlyDictionary<string, object?>? args = null) where T : Window
        {
            var scope = _root.BeginLifetimeScope(b =>
            {
                b.RegisterInstance<IWindowContext>( new WindowContext(args ?? new Dictionary<string, object?>()))
                 .As<IWindowContext>()
                 .SingleInstance();
            });

            T window;
            try
            {
                using(AmbientScope.Push(scope))
                {
                    window = scope.Resolve<T>();
                    DiScope.SetScope(window, scope);
                    window.Owner = GetOwner();
                }
            } catch
            {
                scope.Dispose();
                throw;
            }


            var host = RegisterWindow(scope, window)
                ?? throw new InvalidOperationException("Failed to register window");

            window.Closed += (_, __) =>
            {
                UnregisterWindow(host); // если у вас есть такой метод
                scope.Dispose();
            };
            
            return host.Key;
        }


        public void ShowWindow(Guid windowId)
        {
            var winHost = FindHostWindow(windowId);
            if(winHost is null)
                return;
            if(winHost is WindowHost host)
                host.Window.Show();
        }

        public void CloseWindow(Guid windowId)
        {
            if(IsWindowOpen(windowId))
            {
                var winHost = FindHostWindow(windowId);
                if(winHost is WindowHost win)
                {
                    WinClose(win);
                    UnregisterWindow(win);
                }
            }
        }

        public bool IsWindowOpen(Guid windowId)
        {
            return FindHostWindow(windowId) != null;
        }

        public WindowHost? FindHostWindow(Guid windowId)
        {
            return _windowHosts.ContainsKey(windowId) ? _windowHosts[windowId] : null;
        }



        private WindowHost? RegisterWindow<T>(ILifetimeScope scope, T window) where T : Window
        {
            if(window.DataContext is IWindowWithId withId)
            {
                var host = new WindowHost(withId.WindowId, scope, window);
                _windowHosts[host.Key] = host;
                return host;
            } else
                throw new InvalidOperationException("Window's DataContext must implement IWindowWithId");
        }

        private void UnregisterWindow(WindowHost windowHost)
        {
            if(windowHost is null)
                return;
            if(_windowHosts.ContainsKey(windowHost.Key))
                _windowHosts.Remove(windowHost.Key);

            (windowHost.Window.Parent as Window)?.Focus();
        }

        private void WinClose(WindowHost windowHost)
        {
            if(windowHost is not null)
            {
                windowHost.Window.Owner?.Focus();
                windowHost.Window.Close();
            }
        }

        public void Dispose()
        {
            _root?.Dispose();
        }

    }

}
