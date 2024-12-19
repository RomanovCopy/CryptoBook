using CryptoBook.Infrastructure;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Models
{
    public class HomeModel: ViewModelBase
    {



        public HomeModel()
        {

        }


        internal bool CanExecute_PageLoded(object obj)
        {
            return true;
        }
        internal void Execute_PageLoaded(object obj)
        {
        }


        internal bool CanExecute_PageClear(object obj)
        {
            return true;
        }
        internal void Execute_PageClear(object obj)
        {
        }



        internal bool CanExecute_PageClose(object obj)
        {
            return true;
        }
        internal void Execute_PageClose(object obj)
        {
        }

    }
}
