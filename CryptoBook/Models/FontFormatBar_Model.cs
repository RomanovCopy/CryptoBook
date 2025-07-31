using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.Collections.ObjectModel;
using System.Windows.Documents;
using Drawing = System.Drawing;


namespace CryptoBook.Models
{
    internal class FontFormatBar_Model: ViewModelBase
    {
        private readonly ILifetimeScope scope;


        internal FontFormatBar_Model(ILifetimeScope scope)
        {
            this.scope = scope ?? throw new ArgumentNullException(nameof(scope));
        }






        internal bool CanExecute_Loaded(object? obj) { return true; }
        internal void OnLoaded(object? obj) { }

        internal bool CanExecute_Close(object? obj) { return true; }
        internal void OnClose(object? obj) { }

        internal bool CanExecute_Closing(object? obj) { return true; }
        internal void OnClosing(object? obj) { }

        internal bool CanExecute_Closed(object? obj) { return true; }
        internal void OnClosed(object? obj) { }
    }
}