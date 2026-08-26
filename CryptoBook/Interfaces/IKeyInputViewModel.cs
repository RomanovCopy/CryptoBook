using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IKeyInputViewModel:
        IViewModel,
        IWindowOptions,
        IWindowWithId,
        IDialogResult<bool>
    {
        string Title { get; }
        string Message { get; }
        bool ShowRepeatPassword { get; }

        void SetResult(bool accepted);
    }
}
