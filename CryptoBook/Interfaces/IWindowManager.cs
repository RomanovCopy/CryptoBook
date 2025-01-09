using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CryptoBook.Interfaces
{
    interface IWindowManager
    {

        T CreateWindow<T>() where T : Window;
        void ShowWindow<T>(Guid windowId) where T : Window;
        void CloseWindow<T>(Guid windowId) where T : Window;
        bool IsWindowOpen<T>(Guid windowId) where T : Window;
    }
}
