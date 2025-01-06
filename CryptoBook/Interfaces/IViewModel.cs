using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

using CryptoBook.Infrastructure;

namespace CryptoBook.Interfaces
{
    public interface IViewModel:ICloseable
    {

        public static Action Ready { get; set; }
        public ICommand Loaded { get; }
        public ICommand Close { get; }
        public ICommand Closing { get; }
    }
}
