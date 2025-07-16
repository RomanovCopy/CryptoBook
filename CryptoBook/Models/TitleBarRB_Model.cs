using Autofac;

using CryptoBook.Infrastructure;

namespace CryptoBook.Models
{
    internal class TitleBarRB_Model: ViewModelBase
    {

        private readonly ILifetimeScope scope;

        public TitleBarRB_Model(ILifetimeScope _scope)
        {
            scope = _scope;
        }


        internal bool CanExecute_BoldCommand(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_BoldCommand(object? obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_ItalicCommand(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_ItalicCommand(object? obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_UnderlineCommand(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_UnderlineCommand(object? obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_ClearFormattingCommand(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_ClearFormattingCommand(object? obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_InsertImageCommand(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_InsertImageCommand(object? obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_Loaded(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_Loaded(object? obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_Close(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_Close(object? obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_Closing(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_Closing(object? obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_Closed(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_Closed(object? obj)
        {
            throw new NotImplementedException();
        }
    }
}
