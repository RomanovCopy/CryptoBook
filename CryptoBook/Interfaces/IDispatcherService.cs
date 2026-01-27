using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace CryptoBook.Interfaces
{
    public interface IDispatcherService
    {
        bool CheckAccess();
        void Invoke(Action action);
        void BeginInvoke(Action action);

        Task InvokeAsync(Action action, DispatcherPriority priority = DispatcherPriority.Background);
        Task<T> InvokeAsync<T>(Func<T> func, DispatcherPriority priority = DispatcherPriority.Background);
    }

}
