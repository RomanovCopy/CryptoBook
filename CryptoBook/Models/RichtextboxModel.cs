using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;

using System.Drawing;
using System.Windows.Input;

namespace CryptoBook.Models
{
    internal class RichtextboxModel: ViewModelBase
    {


        public RichtextboxModel(ILifetimeScope scope)
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
            // Здесь можно реализовать логику закрытия, если требуется
        }
        internal bool CanExecute_Closing(object? obj)
        {
            return true;
        }
        internal void Execute_Closing(object? obj)
        {
            // Здесь можно реализовать логику при закрытии, если требуется
        }
        internal bool CanExecute_Closed(object? obj)
        {
            return true;
        }
        internal void Execute_Closed(object? obj)
        {
            // Здесь можно реализовать логику после закрытия, если требуется
        }




    }
}
