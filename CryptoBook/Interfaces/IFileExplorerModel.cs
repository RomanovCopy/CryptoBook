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
    public interface IFileExplorerModel:IModel,IWindowWithId,IWindowOptions
    {
        double LeftColumnPercent { get; set; }
        double RightColumnPercent { get; set; }
        bool IsHiddenFilesVisible { get; set; } 
        string CurrentPath { get; set; }
        ISystemItem? SelectedItem { get; set; }
        ReadOnlyObservableCollection<IDriveItem>GetDrives { get; }

        bool CanExecute_CutCommand(object? obj);
        bool CanExecute_CopyCommand(object? obj);
        bool CanExecute_PasteCommand(object? obj);
        bool CanExecute_DeleteCommand(object? obj);
        bool CanExecute_CreateFileCommand(object? obj);
        bool CanExecute_CreateDirectoryCommand(object? obj);
        bool CanExecute_RenameCommand(object? obj);
        bool CanExecute_MoveCommand(object? obj);
        bool CanExecure_RefreshCommand(object? obj);
        bool CanExecute_TreeViewItemSelectedCommand (object? obj);
        bool CanExecute_ListViewItemDoubleClickCommand(object? obj);
        bool CanExecute_ListViewSelectionChangedCommand(object? obj);
        bool CanExecute_WindowSizeChanged(object? obj);


        void Execute_CutCommand(object? obj);
        void Execute_CopyCommand(object? obj);
        void Execute_PasteCommand(object? obj);
        void Execute_DeleteCommand(object? obj);
        void Execute_CreateFileCommand(object? obj);
        void Execute_CreateDirectoryCommand(object? obj);
        void Execute_RenameCommand(object? obj);
        void Execute_MoveCommand(object? obj);
        void Execute_RefreshCommand(object? obj);
        void Execute_TreeViewItemSelectedCommand (object? obj);
        void Execute_ListViewItemDoubleClickCommand(object? obj);
        void Execute_ListViewSelectionChangedCommand(object? obj);
        void Execute_WindowSizeChanged(object? obj);
    }
}
