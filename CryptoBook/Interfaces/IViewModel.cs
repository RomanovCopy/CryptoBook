using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IViewModel
    {
        public ICommand Loaded(object obj);
        public ICommand Closed(object obj);
        public ICommand Closing(object obj);
    }
}
