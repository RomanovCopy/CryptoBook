using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface ISystemItemName_Editor_ViewModel: IViewModel, IWindowWithId ,IWindowOptions
    {
        string OldName { get; } 
        string OldExtension { get; }
        string NewName { get; set; }
        string NewExtension { get; set; }

        ICommand SaveCommand { get; }
        ICommand RestoreCommand { get; }
    }
}
