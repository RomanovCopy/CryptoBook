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

        public Twindow CreateWindow<Tviewmodel, Twindow>() where Tviewmodel:IViewModel, ICloseable where Twindow : Window
        {
            if(!_scope.IsRegistered<Twindow>())
                throw new InvalidOperationException($"Window of type {typeof(Twindow).Name} is not registered in the container.");
            var vm = _scope.Resolve<Tviewmodel>();
            var window = _scope.Resolve<Twindow>();
            RegisterWindow<Twindow, Tviewmodel>(window, vm);
            return window;
        }

        public void ShowWindow<T>(T viewmodel) where T : IViewModel
        {
            var window = FindWindow(viewmodel);
            window?.Show();
        }

        public void CloseWindow<T>(T viewmodel) where T : IViewModel, ICloseable
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


        private void RegisterWindow<Twindow, Tviewmodel>(Window window, IViewModel viewModel)
        {
            if(viewModel is ICloseable vm)
            {
                vm.RequestClose += (s, e) => WinClose(window);
            }
            window.DataContext = viewModel;
            _openWindows.Add(window);
            window.Closed += (s, e) => UnregisterWindow(window);
        }

        private void UnregisterWindow(Window window)
        {
            if(window.DataContext is ICloseable vm)
            {
                vm.RequestClose -=(s,e)=> WinClose(window);
            }
            _openWindows.Remove(window);
        }

        private void WinClose(Window window)
        {
            window.Close();
        }
    }

}
