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
        void ShowWindow<T>(T viewmodel) where T : IViewModel;
        void CloseWindow<T>(T viewmodel) where T : IViewModel;
        bool IsWindowOpen<T>(T viewmodel) where T :IViewModel;
    }
}
