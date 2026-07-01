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

        private readonly IKeyInputModel _model;

        public Guid WindowId => _model.WindowId;
        public double WindowWidth { get => _model.WindowWidth; set => _model.WindowWidth = value; }
        public double WindowHeight { get => _model.WindowHeight; set => _model.WindowHeight = value; }
        public double WindowTop { get => _model.WindowTop; set => _model.WindowTop = value; }
        public double WindowLeft { get => _model.WindowLeft; set => _model.WindowLeft = value; }
        public WindowState WindowState { get => _model.WindowState; set => _model.WindowState = value; }

        public string Title { get => _model.Title; }
        public string Message { get => _model.Message; }
        public bool ShowRepeatPassword { get => _model.ShowRepeatPassword; }


        public KeyInputViewModel(IKeyInputModel model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
        }



        public ICommand Loaded => loaded ??=new RelayCommand(_model.Execute_Loaded, _model.CanExecute_Loaded);
        RelayCommand loaded;

        public ICommand Close => close ??= new RelayCommand(_model.Execute_Close, _model.CanExecute_Close);
        RelayCommand close;

        public ICommand Closing => closing ??= new RelayCommand(_model.Execute_Closing, _model.CanExecute_Closing);
        RelayCommand closing;

        public ICommand Closed => closed ??= new RelayCommand(_model.Execute_Closed, _model.CanExecute_Closed);
        RelayCommand closed;


    }
}
