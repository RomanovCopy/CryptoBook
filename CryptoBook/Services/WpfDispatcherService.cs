using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace CryptoBook.Services
{
    public class WpfDispatcherService:IDispatcherService
    {
        private readonly Dispatcher _dispatcher;

        public WpfDispatcherService(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }
        public bool CheckAccess()
        {
            return _dispatcher.CheckAccess();
        }

        public void Invoke(Action action)
       => _dispatcher.Invoke(action);

        public void BeginInvoke(Action action)
            => _dispatcher.BeginInvoke(action);

    }
}
