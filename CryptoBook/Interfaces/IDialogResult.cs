using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IDialogResult{ }
    public interface IConditionalDialogResult: IDialogResult
    {
        bool HasResult { get; }
    }
    public interface IDialogResult<out T>: IDialogResult
    {
        T? Result { get; }
    }
}
