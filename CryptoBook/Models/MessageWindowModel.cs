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
    public class MessageWindowModel:ViewModelBase,IMessageWindowModel
    {
        private readonly IMessageService _messageService;

        public Guid WindowId { get => _windowId; private set => SetProperty(ref _windowId, value); }
        Guid _windowId;

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


        public string Title => throw new NotImplementedException();

        public string Message => throw new NotImplementedException();




        public bool CanExecute_OkCommand(object? obj)
        {
            throw new NotImplementedException();
        }

        public void Execute_OkCommand(object? obj)
        {
            throw new NotImplementedException();
        }

        public bool CanExecute_CancelCommand(object? obj)
        {
            throw new NotImplementedException();
        }

        public void Execute_CancelCommand(object? obj)
        {
            throw new NotImplementedException();
        }

        public bool CanExecute_Close(object? obj)
        {
            throw new NotImplementedException();
        }

        public void Execute_Loaded(object? obj)
        {
            throw new NotImplementedException();
        }

        public bool CanExecute_Loaded(object? obj)
        {
            throw new NotImplementedException();
        }

        public void Execute_Close(object? obj)
        {
            throw new NotImplementedException();
        }

        public bool CanExecute_Closing(object? obj)
        {
            throw new NotImplementedException();
        }

        public void Execute_Closing(object? obj)
        {
            throw new NotImplementedException();
        }

        public bool CanExecute_Closed(object? obj)
        {
            throw new NotImplementedException();
        }

        public void Execute_Closed(object? obj)
        {
            throw new NotImplementedException();
        }

    }
}
