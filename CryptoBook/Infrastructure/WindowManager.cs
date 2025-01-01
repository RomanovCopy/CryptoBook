using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CryptoBook.Interfaces;

namespace CryptoBook.Infrastructure
{
    class WindowManager:IWindowManager
    {


        public void ShowWindow<ViewModel>() where ViewModel : class
        {
            throw new NotImplementedException();
        }

        public void ShowDialog<ViewModel>() where ViewModel : class
        {
            throw new NotImplementedException();
        }

        public void CloseWindow<ViewModel>() where ViewModel : class
        {
            throw new NotImplementedException();
        }
    }
}
