using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace CryptoBook.Services
{
    public sealed class WpfDispatcherService: IDispatcherService
    {
        private readonly Dispatcher _dispatcher;

        public WpfDispatcherService(Dispatcher dispatcher) => _dispatcher = dispatcher;

        public bool CheckAccess() => _dispatcher.CheckAccess();

        public void Invoke(Action action)
        {
            if(CheckAccess())
                action();
            else
                _dispatcher.Invoke(action);
        }

        public void BeginInvoke(Action action)
        {
            if(CheckAccess())
                action();
            else
                _dispatcher.BeginInvoke(action);
        }

        public Task InvokeAsync(Action action, DispatcherPriority priority = DispatcherPriority.Background)
        {
            if(CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }
            return _dispatcher.InvokeAsync(action, priority).Task;
        }

        public Task<T> InvokeAsync<T>(Func<T> func, DispatcherPriority priority = DispatcherPriority.Background)
        {
            if(CheckAccess())
                return Task.FromResult(func());
            return _dispatcher.InvokeAsync(func, priority).Task;
        }
    }

}
