using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Services
{
    public sealed class MessageService: IMessageService
    {
        private readonly IWindowManager _windowManager;

        public MessageService( IWindowManager windowManager)
        {
            _windowManager = windowManager;      
        }

        public void ShowMessage( string title, string message)
        {
            using var scope =
                _rootScope.BeginLifetimeScope(builder =>
                {
                    builder.RegisterInstance(
                        new MessageDialogParameters(
                            title,
                            message));
                });

            _windowManager.ShowDialog<MessageDialogWindow>(scope);
        }

        public bool ShowConfirmation(
            string title,
            string message)
        {
            using var scope =
                _rootScope.BeginLifetimeScope(builder =>
                {
                    builder.RegisterInstance(
                        new MessageDialogParameters(
                            title,
                            message));
                });

            return _windowManager
                .ShowDialog<MessageDialogWindow>(scope) == true;
        }
    }
}
