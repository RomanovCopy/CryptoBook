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
        private readonly IWindowContext _windowContext;
        public bool Result { get=>_result; private set => SetProperty(ref _result, value); }
        bool _result;
        public Guid WindowId { get => _windowId; private set => SetProperty(ref _windowId, value); }
        Guid _windowId;
        public bool IsCanceled { get => _isCanceled; private set => SetProperty(ref _isCanceled, value); }
        bool _isCanceled;

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


        public string Title { get=> _title; private set => SetProperty(ref _title, value); }
        string _title;
        public string Message { get => _message; private set => SetProperty(ref _message, value); }
        string _message;


        public MessageWindowModel(IMessageService messageService, IWindowContext windowContext)
        {
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            _windowContext = windowContext ?? throw new ArgumentNullException(nameof(windowContext));
            WindowId= Guid.NewGuid();
            WindowWidth = Properties.Settings.Default.MessageWindowWidth;
            WindowHeight= Properties.Settings.Default.MessageWindowHeight;
            WindowLeft=Properties.Settings.Default.MessageWindowLeft;
            WindowTop=Properties.Settings.Default.MessageWindowTop;
            if(windowContext is IWindowContext context)
            {
                Title = context.Get<string>("Title");
                Message = context.Get<string>("Message");
                IsCanceled = context.Get<bool>("IsCanceled");
            }
            Result = false;
        }


        public bool CanExecute_OkCommand(object? obj)
        {
            return true;
        }
        public void Execute_OkCommand(object? obj)
        {
            Result = true;  
            _messageService.CloseDialog(WindowId);
        }

        public bool CanExecute_CancelCommand(object? obj)
        {
            return true;
        }
        public void Execute_CancelCommand(object? obj)
        {
            Result = false;
            _messageService.CloseDialog(WindowId);
        }


        public void Execute_Loaded(object? obj)
        {
            throw new NotImplementedException();
        }
        public bool CanExecute_Loaded(object? obj)
        {
            throw new NotImplementedException();
        }

        public bool CanExecute_Close(object? obj)
        {
            throw new NotImplementedException();
        }
        public void Execute_Close(object? obj)
        {
            throw new NotImplementedException();
        }

        public bool CanExecute_Closing(object? obj)
        {
            return true;
        }
        public void Execute_Closing(object? obj)
        {
            var set=Properties.Settings.Default;
            set.MessageWindowWidth=WindowWidth;
            set.MessageWindowHeight=WindowHeight;
            set.MessageWindowLeft=WindowLeft;
            set.MessageWindowTop=WindowTop;
            set.Save();
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
