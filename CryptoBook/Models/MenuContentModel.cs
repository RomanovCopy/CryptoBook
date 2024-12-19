using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CryptoBook.Infrastructure;

namespace CryptoBook.Models
{
    internal class MenuContentModel: ViewModelBase
    {
        internal MenuContentModel()
        {

        }
        internal bool CanExecute_Reading(object obj)
        {
            return true;
        }
        internal void Execute_Reading(object obj)
        {
        }

        internal bool CanExecute_InsertImage(object obj)
        {
            return true;
        }
        internal void Execute_InsertImage(object obj)
        {
        }

        internal bool CanExecute_InsertText(object obj)
        {
            return true;
        }
        internal void Execute_InsertText(object obj)
        {
        }

        internal bool CanExecute_OpenDocumentTree(object obj)
        {
            return true;
        }
        internal void Execute_OpenDocumentTree(object obj)
        {
        }

        internal bool CanExecute_MediaPlayer(object obj)
        {
            return true;
        }
        internal void Execute_MediaPlayer(object obj)
        {
        }

        internal bool CanExecute_Loaded(object obj)
        {
            return true;
        }

        internal void Execute_Loaded(object obj)
        {
        }

        internal bool CanExecute_Close(object obj)
        {
            return true;
        }
        internal void Execute_Close(object obj)
        {
        }

        internal bool CanExecute_Closing(object obj)
        {
            return true;
        }
        internal void Execute_Closing(object obj)
        {
        }
    }
}
