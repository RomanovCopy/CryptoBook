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
    public sealed class KeyInputViewModel :ViewModelBase, IKeyInputViewModel
    {

        public double WindowWidth { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public double WindowHeight { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public double WindowTop { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public double WindowLeft { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public WindowState WindowState { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public Guid WindowId => throw new NotImplementedException();

        public KeyInputViewModel()
        {
            
        }


        public string Title { get; } = "Ключ шифрования";
        public string Message { get; } = "Введите ключ шифрования:";

        public bool ShowRepeatPassword { get; init; } = true;

        public ICommand Loaded => throw new NotImplementedException();

        public ICommand Close => throw new NotImplementedException();

        public ICommand Closing => throw new NotImplementedException();

        public ICommand Closed => throw new NotImplementedException();


    }
}
