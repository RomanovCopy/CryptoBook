using CryptoBook.Infrastructure;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Models
{
    public class TitleBarModel:ViewModelBase
    {
        public TitleBarModel()
        {

        }

        internal bool CanExecute_Loaded(object obj)
        {
            return true;
        }
        internal void Execute_Loaded(object obj)
        {

        }

        internal bool CanExecute_TitleBarMouseLeftButtonDown(object obj)
        {
            return true;
        }
        internal void Execute_TitleBarMouseLeftButtonDown(object obj)
        {
            throw new NotImplementedException();
        }

        internal bool CanExecute_TitleBarMouseMove(object obj)
        {
            return true;
        }
        internal void Execute_TitleBarMouseMove(object obj)
        {
            throw new NotImplementedException();
        }

        internal bool CanExecute_ButtonBack_Click(object obj)
        {
            return true;
        }
        internal void Execute_ButtonBack_Click(object obj)
        {
            throw new NotImplementedException();
        }

        internal bool CanExecute_ButtonForward_Click(object obj)
        {
            return true;
        }
        internal void Execute_ButtonForward_Click(object obj)
        {
            throw new NotImplementedException();
        }

        internal bool CanExecute_ToggleMenu_Click(object obj)
        {
            return true;
        }
        internal void Execute_ToggleMenu_Click(object obj)
        {
            throw new NotImplementedException();
        }

        internal bool CanExecute_MinButtonClick(object obj)
        {
            return true;
        }
        internal void Execute_MinButtonClick(object obj)
        {
            throw new NotImplementedException();
        }

        internal bool CanExecute_MaxButtonClick(object obj)
        {
            return true;
        }
        internal void Execute_MaxButtonClick(object obj)
        {
            throw new NotImplementedException();
        }

        internal bool CanExecute_CloseButtonClick(object obj)
        {
            return true;
        }
        internal void Execute_CloseButtonClick(object obj)
        {
            throw new NotImplementedException();
        }

        internal bool CanExecute_Closed(object obj)
        {
            return true;
        }
        internal void Execute_Closed(object obj)
        {
            throw new NotImplementedException();
        }

        internal bool CanExecute_Closing(object obj)
        {
            return true;
        }
        internal void Execute_Closing(object obj)
        {
            throw new NotImplementedException();
        }
    }
}
