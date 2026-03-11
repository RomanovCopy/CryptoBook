using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IMessageService
    {
        void ShowMessage( string title, string message);
        bool ShowConfirmation( string title, string message);
    }
}
