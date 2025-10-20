using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

namespace CryptoBook.Models
{
    internal class TimeWebAIPageModel: ViewModelBase,ITimeWebAIPageModel
    {
        public TimeWebAIPageModel()
        {
        }



        public bool CanExecute_PageLoaded(object? obj)
        {
            return true;
        }
        public void Execute_PageLoaded(object? obj)
        {
        }

        public bool CanExecute_PageClear(object? obj)
        {
            return true;
        }
        public void Execute_PageClear(object? obj)
        {
        }

        public bool CanExecute_Loaded(object? obj)
        {
            return true;
        }
        public void Execute_Loaded(object? obj)
        {

        }

        public bool CanExecute_Close(object? obj)
        {
            return true;
        }
        public void Execute_Close(object? obj)
        {

        }

        public bool CanExecute_Closing(object? obj)
        {
            return true;
        }
        public void Execute_Closing(object? obj)
        {

        }

        public bool CanExecute_Closed(object? obj)
        {
            return true;
        }
        public void Execute_Closed(object? obj)
        {
        }

    }
}
