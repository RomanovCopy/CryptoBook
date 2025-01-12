using CryptoBook.Infrastructure;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Models
{
    internal class MenuEncryptionModel: ViewModelBase
    {


        public MenuEncryptionModel()
        {

        }

        internal bool CanExecute_DeleteKey(object? obj)
        {
            return true;
        }
        internal void Execute_DeleteKey(object? obj)
        {
        }

        internal bool CanExecute_InstalKey(object? obj)
        {
            return true;
        }
        internal void Execute_InstalKey(object? obj)
        {
        }

        internal bool CanExecute_Encrypt(object? obj)
        {
            return true;
        }
        internal void Execute_Encrypt(object? obj)
        {
        }

        internal bool CanExecute_Decrypt(object? obj)
        {
            return true;
        }
        internal void Execute_Decrypt(object? obj)
        {
        }

        internal bool CanExecute_Loaded(object? obj)
        {
            return true;
        }
        internal void Execute_Loaded(object? obj)
        {
        }

        internal bool CanExecute_Close(object? obj)
        {
            return true;
        }
        internal void Execute_Close(object? obj)
        {
        }

        internal bool CanExecute_Closing(object? obj)
        {
            return true;
        }
        internal void Execute_Closing(object? obj)
        {
        }


    }
}
