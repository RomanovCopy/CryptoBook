using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IMessageService:IService
    {
        Task<Guid> ShowMessage(string title, string message, bool isCanceled=false);
        void CloseDialog(Guid id);
        bool ShowConfirmation( Guid id);
    }
}
