using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

namespace CryptoBook.Models
{
    internal class MyMessageBox_Model:ViewModelBase
    {
        private readonly ILifetimeScope scope;
        private readonly IWindowManager windowManager;

        internal string Header { get => header; set => SetProperty(ref header, value); }
        private string header;

        internal string Message { get => message; set => SetProperty(ref message, value); }
        private string message;

        public MyMessageBox_Model(ILifetimeScope scope)
        {
            this.scope = scope;
            windowManager = scope.Resolve<IWindowManager>();
        }



        internal bool CanExecute_SetMessage(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_SetMessage(object? obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_SetHeader(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_SetHeader(object? obj)
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


        internal bool CanExecute_Closed(object? obj)
        {
            throw new NotImplementedException();
        }

        internal void Execute_Closed(object? obj)
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

    }
}
