using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using Autofac;

using CryptoBook.Injections;
using CryptoBook.Interfaces;

namespace CryptoBook.Infrastructure
{
    class WindowManager: IWindowManager
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
            var window = _scope.Resolve<T>();
            RegisterWindow(window);
            return window;
        }

        public void ShowWindow<T>(T window) where T : Window
        {
            if(!_openWindows.Contains(window))
            {
                RegisterWindow(window);
            }
            window.Show();
        }

        public void CloseWindow<T>(T window) where T : Window
        {
            if(_openWindows.Contains(window))
            {
                window.Close();
                UnregisterWindow(window);
            }
        }

        public bool IsWindowOpen<T>(T window) where T : Window
        {
            return _openWindows.Contains(window);
        }

        private void RegisterWindow<T>(T window) where T : Window
        {
            _openWindows.Add(window);
            window.Closed += (s, e) => UnregisterWindow(window);
        }

        private void UnregisterWindow<T>(T window) where T : Window
        {
            _openWindows.Remove(window);
        }
    }
}
