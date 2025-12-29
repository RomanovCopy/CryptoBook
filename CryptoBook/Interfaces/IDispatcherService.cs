using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IDispatcherService
    {
        bool CheckAccess();
        void Invoke(Action action);
        void BeginInvoke(Action action);
    }
}
