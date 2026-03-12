using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IMessageWindowViewModel:IViewModel,IWindowWithId,IWindowOptions,IDialogResult<bool>
    {
        string? Title { get; }
        string? Message {  get; }
        bool IsCanceled { get; }

        ICommand OkCommand { get; }
        ICommand CancelCommand { get; }
    }
}
