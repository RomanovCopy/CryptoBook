using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class SideMenuViewModel:ViewModelBase, ISideMenuViewModel
    {
        public SideMenuViewModel()
        {
        }

        public ICommand Loaded => throw new NotImplementedException();

        public ICommand Close => throw new NotImplementedException();

        public ICommand Closing => throw new NotImplementedException();
    }
}
