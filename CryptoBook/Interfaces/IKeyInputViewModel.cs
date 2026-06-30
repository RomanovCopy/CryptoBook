using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IKeyInputViewModel: IViewModel,IWindowOptions, IWindowWithId
    {
        string Title { get; }
        string Message { get; }
        bool ShowRepeatPassword { get; init; }

    }
}
