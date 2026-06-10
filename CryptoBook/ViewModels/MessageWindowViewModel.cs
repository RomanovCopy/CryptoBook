using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class MessageWindowViewModel:ViewModelBase,IMessageWindowViewModel
    {
        private readonly IMessageWindowModel _model;
        public Guid WindowId => _model.WindowId;
        public bool Result => _model.Result;
        public bool IsCanceled => _model.IsCanceled;

        public double WindowWidth { get => _model.WindowWidth; set => _model.WindowWidth = value; }
        public double WindowHeight { get => _model.WindowHeight; set => _model.WindowHeight = value; }
        public double WindowTop { get => _model.WindowTop; set => _model.WindowTop = value; }
        public double WindowLeft { get => _model.WindowLeft; set => _model.WindowLeft=value; }
        public WindowState WindowState { get => _model.WindowState; set => _model.WindowState=value; }

        public string? Title => _model.Title;
        public string? Message => _model.Message;

        public MessageWindowViewModel(IMessageWindowModel? model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _model.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }


        public ICommand OkCommand => _okCommand??=new RelayCommand(_model.Execute_OkCommand,_model.CanExecute_OkCommand);
        RelayCommand _okCommand;
        public ICommand CancelCommand => _cancelCommand??=new RelayCommand(_model.Execute_CancelCommand, _model.CanExecute_CancelCommand);
        RelayCommand _cancelCommand;

        public ICommand Loaded => _loaded??=new RelayCommand(_model.Execute_Loaded, _model.CanExecute_Loaded);
        RelayCommand _loaded;
        public ICommand Close => _close??=new RelayCommand(_model.Execute_Close, _model.CanExecute_Close);
        RelayCommand _close;
        public ICommand Closing => _closing??=new RelayCommand(_model.Execute_Closing, _model.CanExecute_Closing);
        RelayCommand _closing;
        public ICommand Closed => _closed??=new RelayCommand(_model.Execute_Closed, _model.CanExecute_Closed);
        RelayCommand _closed;

    }
}
