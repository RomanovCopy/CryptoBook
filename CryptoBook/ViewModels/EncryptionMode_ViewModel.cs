using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class EncryptionMode_ViewModel:ViewModelBase, IEncryptionMode_ViewModel
    {

        public Guid WindowId => throw new NotImplementedException();

        public double WindowWidth { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public double WindowHeight { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public double WindowTop { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public double WindowLeft { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public WindowState WindowState { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }


        public EncryptionMode_ViewModel()
        {
        }




        public ICommand Loaded => throw new NotImplementedException();

        public ICommand Close => throw new NotImplementedException();

        public ICommand Closing => throw new NotImplementedException();

        public ICommand Closed => throw new NotImplementedException();
    }
}
