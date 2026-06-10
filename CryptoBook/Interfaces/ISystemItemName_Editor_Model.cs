using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface ISystemItemName_Editor_Model: IModel, IWindowWithId, IWindowOptions
    {
        string OldName { get; }
        string OldExtension { get; }
        string NewName { get; set; }
        string NewExtension { get; set; }

        bool CanExecute_SaveCommand(object? obj);
        void Execute_SaveCommand(object? obj);
        bool CanExecute_RestoreCommand(object? obj);
        void Execute_RestoreCommand(object? obj);
    }
}
