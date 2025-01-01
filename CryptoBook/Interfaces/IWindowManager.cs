using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IWindowManager
    {
        void ShowWindow<ViewModel>() where ViewModel : class;
        void ShowDialog<ViewModel>() where ViewModel : class;
        void CloseWindow<ViewModel>() where ViewModel : class;
    }
}
