using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CryptoBook.Interfaces
{
    public interface IWindowManager
    {
        T CreateWindow<T>() where T : Window;
        void ShowWindow<T>(T window) where T : Window;
        void CloseWindow<T>(T window) where T : Window;
        bool IsWindowOpen<T>(T window) where T : Window;
    }
}
