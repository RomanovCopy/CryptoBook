using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Views;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Models
{
    public class TitleBarModel: ViewModelBase
    {
        /// <summary>
        /// окно перемещается
        /// </summary>
        private bool _isDragging;

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

        internal bool CanExecute_MouseLeftButtonDown(object obj)
        {
            return true;
        }
        internal void Execute_MouseLeftButtonDown(object obj)
        {
            if(!_isDragging)
            {
                _isDragging = true;
                App.Container.Resolve<MainWindow>().DragMove();
                _isDragging = false;
            }
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

        internal bool CanExecute_GoToWindow(object obj)
        {
            return false;
        }
        internal void Execute_GoToWindow(object obj)
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
