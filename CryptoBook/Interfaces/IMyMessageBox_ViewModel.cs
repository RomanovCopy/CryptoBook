using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    interface IMyMessageBox_ViewModel:IViewModel
    {
        public ICommand SetHeader { get; }

        public ICommand SetMessage { get; }

    }
}
