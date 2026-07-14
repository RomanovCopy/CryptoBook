using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CryptoBook.Models
{
    public class EncryptionMode_Model: ViewModelBase, IEncryptionMode_Model
    {

        public Guid WindowId { get; private set; }


        public double WindowWidth { get => _windowWidth; set => SetProperty(ref _windowWidth, value); }
        double _windowWidth;
        public double WindowHeight { get => _windowHeight; set => SetProperty(ref _windowHeight, value); }
        double _windowHeight;
        public double WindowTop { get => _windowTop; set => SetProperty(ref _windowTop, value); }
        double _windowTop;
        public double WindowLeft { get => _windowLeft; set => SetProperty(ref _windowLeft, value); }
        double _windowLeft;
        public WindowState WindowState { get => _windowState; set => SetProperty(ref _windowState, value); }
        WindowState _windowState;


        public string Title { get => _title; private set => SetProperty(ref _title, value); }
        string _title;

        public string MessageMode { get => _messageMode; private set => SetProperty(ref _messageMode, value); }
        string _messageMode;

        public string MessageModeTop { get => _messageModeTop; private set => SetProperty(ref _messageModeTop, value); }
        string _messageModeTop;

        public string MessageModeBottom { get => _messageModeBottom; private set => SetProperty(ref _messageModeBottom, value); }
        string _messageModeBottom;

        public string Path { get => _path; private set => SetProperty(ref _path, value); }
        string _path;

        public string WarningMessage { get => _warningMessage; private set => SetProperty(ref _warningMessage, value); }
        string _warningMessage;




        public EncryptionMode_Model()
        {
            WindowId = Guid.NewGuid();
            WindowWidth = 420;
            WindowHeight = 230;
        }



        public bool CanExecute_Loaded(object? obj)
        {
            return true;
        }
        public void Execute_Loaded(object? obj)
        {
            throw new NotImplementedException();
        }


        public bool CanExecute_Closing(object? obj)
        {
            return true;
        }
        public void Execute_Closing(object? obj)
        {
            throw new NotImplementedException();
        }



        public bool CanExecute_Close(object? obj)
        {
            return true;
        }
        public void Execute_Close(object? obj)
        {
            throw new NotImplementedException();
        }



        public bool CanExecute_Closed(object? obj)
        {
            return true;
        }

        public void Execute_Closed(object? obj)
        {
            throw new NotImplementedException();
        }

    }
}
