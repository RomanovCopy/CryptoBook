using DTO = CryptoBook.DTO;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IMenuFileModel:IModel
    {
        public DTO.MenuItem CreateItem(string name, DTO.CommandKey commandKey);

        public bool CanExecute_NewFile(object? obj);
        public void Execute_NewFile(object? obj);

        public bool CanExecute_OpenFile(object? obj);
        public void Execute_OpenFile(object? obj);

        public bool CanExecute_SaveFile(object? obj);
        public void Execute_SaveFile(object? obj);

        public bool CanExecute_SaveAsFile(object? obj);
        public void Execute_SaveAsFile(object? obj);


        public bool CanExecute_FileOverview(object? obj);
        public void Execute_FileOverview(object? obj);

        public bool CanExecute_OpenDirectory(object? obj);

        public void Execute_OpenDirectory(object? obj);

        public bool CanExecute_WorkingDirectorySynchronization(object? obj);

        public void Execute_WorkingDirectorySynchronization(object? obj);

        public bool CanExecute_CloseFile(object? obj);
        public void Execute_CloseFile(object? obj);

        public bool CanExecute_UpdateFile(object? obj);
        public void Execute_UpdateFile(object? obj);

        
    }
}
