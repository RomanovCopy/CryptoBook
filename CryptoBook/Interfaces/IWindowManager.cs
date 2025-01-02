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
        void ShowWindow<TWindow, TViewModel>() where TWindow : Window where TViewModel : class;
        void ShowDialog<TWindow, TViewModel>() where TWindow : Window where TViewModel : class;
        void CloseWindow<TViewModel>() where TViewModel : class;
    }
}
