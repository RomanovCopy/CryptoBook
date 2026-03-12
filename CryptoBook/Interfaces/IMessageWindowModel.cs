using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IMessageWindowModel: IModel, IWindowWithId,IWindowOptions,IDialogResult<bool>
    {
        string Title { get; }
        string Message { get; }
        bool IsCanceled { get; }

        bool CanExecute_OkCommand(object? obj);
        void Execute_OkCommand(object? obj);

        bool CanExecute_CancelCommand(object? obj);
        void Execute_CancelCommand(object? obj);
    }
}
