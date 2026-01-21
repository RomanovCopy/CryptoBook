using CryptoBook.DTO;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        ISystemItem SelectedItem { get; set; }
        ReadOnlyObservableCollection<IDriveItem>GetDrives { get; }

        bool CanExecute_CutCommand(object? obj);
        bool CanExecute_CopyCommand(object? obj);
        bool CanExecute_PasteCommand(object? obj);
        bool CanExecute_DeleteCommand(object? obj);
        bool CanExecute_CreateFileCommand(object? obj);
        bool CanExecute_CreateDirectoryCommand(object? obj);
        bool CanExecute_RenameCommand(object? obj);
        bool CanExecute_MoveCommand(object? obj);
        bool CanExecute_TreeViewItemSelectedCommand (object? obj);


        void Execute_CutCommand(object? obj);
        void Execute_CopyCommand(object? obj);
        void Execute_PasteCommand(object? obj);
        void Execute_DeleteCommand(object? obj);
        void Execute_CreateFileCommand(object? obj);
        void Execute_CreateDirectoryCommand(object? obj);
        void Execute_RenameCommand(object? obj);
        void Execute_MoveCommand(object? obj);
        void Execute_TreeViewItemSelectedCommand (object? obj);
    }
}
