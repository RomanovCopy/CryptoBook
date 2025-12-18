using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IFileExplorerModel:INotifyPropertyChanged, IModel,IWindowWithId
    {
        bool IsHiddenFilesVisible { get; set; } 
        string CurrentPath { get; set; }
        List<string> GetFiles { get; }
        List<string> GetDirectories { get;}

        bool CanExecute_CreateFile(object? obj);
        bool CanExecute_CreateDirectory(object? obj);
        bool CanExecute_RenameFile(object? obj);
        bool CanExecute_DeleteFile(object? obj);
        bool CanExecute_DeleteDirectory(object? obj);
        bool CanExecute_MoveFile(object? obj);
        bool CanExecute_MoveDirectory(object? obj);

        void Execute_CreateFile(object? obj);
        void Execute_CreateDirectory(object? obj);
        void Execute_RenameFile(object? obj);
        void Execute_DeleteFile(object? obj);
        void Execute_DeleteDirectory(object? obj);
        void Execute_MoveFile(object? obj);
        void Execute_MoveDirectory(object? obj);
    }
}
