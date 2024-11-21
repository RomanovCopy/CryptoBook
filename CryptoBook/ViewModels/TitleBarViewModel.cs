using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class TitleBarViewModel:ViewModelBase, ITitleBarViewModel
    {
        private readonly TitleBarModel titleBarModel;


        public TitleBarViewModel()
        {
        }






        public ICommand Loaded => throw new NotImplementedException();

        public ICommand Closed => throw new NotImplementedException();

        public ICommand Closing => throw new NotImplementedException();

        public ICommand TitleBarMouseLeftButtonDown => throw new NotImplementedException();

        public ICommand TitleBarMouseMove => throw new NotImplementedException();

        public ICommand ButtonBack_Click => throw new NotImplementedException();

        public ICommand ButtonForward_Click => throw new NotImplementedException();

        public ICommand ToggleMenu_Click => throw new NotImplementedException();

        public ICommand MinButtonClick => throw new NotImplementedException();

        public ICommand MaxButtonClick => throw new NotImplementedException();

        public ICommand CloseButtonClick => throw new NotImplementedException();
    }
}
