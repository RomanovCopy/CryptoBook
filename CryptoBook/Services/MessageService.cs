using CryptoBook.Interfaces;
using CryptoBook.Views;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Services
{
    public sealed class MessageService: IMessageService
    {
        private readonly IWindowManager _windowManager;

        public MessageService(IWindowManager windowManager)
        {
            _windowManager = windowManager;
        }

        public Task<Guid> ShowMessage(string title, string message, bool isCanceled = false)
        {
            if(string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(title));
            if(string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(message));
            var parameters = new Dictionary<string, object?>
            {
                { "Title", title },
                { "Message", message },
                {"IsCanceled",isCanceled }
            };
            var id = _windowManager.CreateWindow<MessageWindow>(args: parameters);
            _windowManager.ShowWindowDialog(id);
            return Task.FromResult(id);
        }

        public void CloseDialog(Guid id)
        {
            _windowManager.CloseWindow(id);
        }

        public bool ShowConfirmation(Guid id)
        {
            return _windowManager.GetResult(id);
        }
    }
}
