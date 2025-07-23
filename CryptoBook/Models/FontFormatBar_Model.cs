using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

namespace CryptoBook.Models
{
    internal class FontFormatBar_Model: ViewModelBase
    {
        private readonly ILifetimeScope scope;
        private readonly IRichtextboxViewModel richtextbox;
        internal FontFormatBar_Model(ILifetimeScope scope)
        {
            this.scope = scope ?? throw new ArgumentNullException(nameof(scope));
            richtextbox= scope.Resolve<IRichtextboxViewModel>() ?? throw new ArgumentNullException(nameof(IRichtextboxViewModel));
        }
        internal bool CanExecute_Bold(object? obj) 
        {
            return richtextbox.BoldCommand.CanExecute(obj);
        }
        internal void Execute_Bold(object? obj) 
        {
            richtextbox.BoldCommand.Execute(obj);
        }

        internal bool CanExecute_Italic(object? obj) 
        { 
            return richtextbox.ItalicCommand.CanExecute(obj); 
        }
        internal void Execute_Italic(object? obj) 
        {
            richtextbox.ItalicCommand.Execute(obj);
        }

        internal bool CanExecute_Underline(object? obj) 
        {
            return richtextbox.UnderlineCommand.CanExecute(obj); 
        }
        internal void Execute_Underline(object? obj) 
        {
            richtextbox.UnderlineCommand.Execute(obj);
        }

        internal bool CanExecute_ClearFormatting(object? obj) { return true; }
        internal void Execute_ClearFormatting(object? obj) { }

        internal bool CanExecute_ChangeFontSize(object? obj) { return true; }
        internal void Execute_ChangeFontSize(object? obj) { }

        internal bool CanExecute_ChangeFontFamily(object? obj) { return true; }
        internal void Execute_ChangeFontFamily(object? obj) { }

        internal bool CanExecute_ChangeTextAlignment(object? obj) { return true; }
        internal void Execute_ChangeTextAlignment(object? obj) { }

        internal bool CanExecute_ChangeForeground(object? obj) { return true; }
        internal void Execute_ChangeForeground(object? obj) { }

        internal bool CanExecute_ChangeBackground(object? obj) { return true; }
        internal void Execute_ChangeBackground(object? obj) { }

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