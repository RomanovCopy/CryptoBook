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
        private readonly Dictionary<Type, Window> _openWindows = new();

        public WindowManager(ILifetimeScope scope)
        {
            _scope = scope;
        }

        public void ShowWindow<TWindow, TViewModel>() where TWindow : Window where TViewModel : class
        {
            if(_openWindows.ContainsKey(typeof(TViewModel)))
            {
                _openWindows[typeof(TViewModel)].Activate();
                return;
            }

            var window = CreateWindow<TWindow, TViewModel>();
            window.Show();
            _openWindows[typeof(TViewModel)] = window;
        }

        public void ShowDialog<TWindow, TViewModel>() where TWindow : Window where TViewModel : class
        {
            var window = CreateWindow<TWindow, TViewModel>();
            window.ShowDialog();
        }

        public void CloseWindow<TViewModel>() where TViewModel : class
        {
            if(_openWindows.TryGetValue(typeof(TViewModel), out var window))
            {
                window.Close();
                _openWindows.Remove(typeof(TViewModel));
            }
        }

        private Window CreateWindow<TWindow, TViewModel>() where TWindow : Window where TViewModel : class
        {
            var viewModel = _scope.Resolve<TViewModel>();
            var window = _scope.Resolve<TWindow>(); // Зарегистрируйте свои окна в контейнере
            //window.DataContext = viewModel;
            return window;
        }
    }
}
