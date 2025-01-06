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
    public class WindowManager: IWindowManager
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
            if(window.DataContext is IViewModel vm)
            {
                vm.RequestClose += (s, e) => window.Close();
            }
            RegisterWindow(window);
            return window;
        }

        public void ShowWindow<T>(T viewmodel) where T : IViewModel
        {
            var window = FindWindow(viewmodel);
            window?.Show();
        }

        public void CloseWindow<T>(T viewmodel) where T : IViewModel
        {
            var window = FindWindow(viewmodel);
            if(window != null)
            {
                window.Close();
                UnregisterWindow(window);
            }
        }

        public bool IsWindowOpen<T>(T viewmodel) where T : IViewModel
        {
            return FindWindow(viewmodel) != null;
        }

        private Window? FindWindow<T>(T viewmodel) where T : IViewModel
        {
            return _openWindows.FirstOrDefault(w => ReferenceEquals(w.DataContext, viewmodel));
        }


        private void RegisterWindow(Window window)
        {
            _openWindows.Add(window);
            window.Closed += (s, e) => UnregisterWindow(window);
        }

        private void UnregisterWindow(Window window)
        {
            _openWindows.Remove(window);
        }
    }

}
