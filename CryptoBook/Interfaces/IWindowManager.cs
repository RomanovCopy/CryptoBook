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
        
        public T CreateWindow<T>() where T : Window;
        void ShowWindow<T>() where T : Window;
        void CloseWindow<T>(T viewmodel) where T : IViewModel, ICloseable;
        bool IsWindowOpen<T>(T viewmodel) where T :IViewModel;
    }
}
