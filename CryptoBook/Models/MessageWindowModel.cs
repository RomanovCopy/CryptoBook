using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Models
{
    public class MessageWindowModel:ViewModelBase,IMessageWindowModel
    {
        private readonly IMessageService _messageService;

        public Guid WindowId { get => _windowId; private set => SetProperty(ref _windowId, value); }
        Guid _windowId;

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
