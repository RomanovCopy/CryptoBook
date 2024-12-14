using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CryptoBook.Infrastructure;

namespace CryptoBook.Models
{
    internal class MenuFileModel:ViewModelBase
    {



        internal MenuFileModel()
        {
        }

        internal bool CanExecute_NewFile(object obj)
        {
            return true;
        }
        internal void Execute_NewFile(object obj)
        {
            throw new NotImplementedException();
        }

        internal bool CanExecute_OpenFile(object obj)
        {
            return true;
        }
        internal void Execute_OpenFile(object obj)
        {

        }

        internal bool CanExecute_SaveFile(object obj)
        {
            return true;
        }
        internal void Execute_SaveFile(object obj)
        {
            throw new NotImplementedException();
        }

        internal bool CanExecute_SaveAsFile(object obj)
        {
            return true;
        }
        internal void Execute_SaveAsFile(object obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_FileOverview(object obj)
        {
            return true;
        }
        internal void Execute_FileOverview(object obj)
        {
            throw new NotImplementedException();
        }

        internal bool CanExecute_OpenDirectory(object obj)
        {
            return true;
        }

        internal void Execute_OpenDirectory(object obj)
        {
            throw new NotImplementedException();
        }

        internal bool CanExecute_WorkingDirectorySynchronization(object obj)
        {
            return true;
        }

        internal void Execute_WorkingDirectorySynchronization(object obj)
        {
            throw new NotImplementedException();
        }

        internal bool CanExecute_CloseFile(object obj)
        {
            return true;
        }
        internal void Execute_CloseFile(object obj)
        {
            throw new NotImplementedException();
        }

        internal bool CanExecute_UpdateFile(object obj)
        {
            return true;
        }
        internal void Execute_UpdateFile(object obj)
        {
            throw new NotImplementedException();
        }
    }
}
