using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IMessageWindowViewModel:IViewModel,IWindowWithId,IWindowOptions
    {
        string? Title { get; }
        string? Message {  get; }

        ICommand OkCommand { get; }
        ICommand CancelCommand { get; }
    }
}
