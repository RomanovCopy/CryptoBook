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
        bool IsFlatViewEnabled { get; set; }
        string CurrentPath { get; }
        string AddressText { get; set; }
        bool IsCurrentDirectoryUnavailable { get; }
        FileExplorerNavigationErrorKind? LastNavigationError { get; }
        string NavigationErrorMessage { get; }
        ISystemItem? SelectedItem { get; set; }
        ISystemItem? SelectedListItem { get; set; }
        IReadOnlyList<ISystemItem> SelectedItemsSnapshot { get; set; }
        ReadOnlyObservableCollection<IDriveItem>GetDrives { get; }
        Task<bool> NavigateAsync(
            string path,
            FileExplorerNavigationMode mode,
            CancellationToken cancellationToken = default);
        Task RestoreLastDirectoryAsync(
            CancellationToken cancellationToken = default);

        bool CanExecute_BackCommand(object? obj);
        bool CanExecute_ForwardCommand(object? obj);
        bool CanExecute_UpCommand(object? obj);
        bool CanExecute_ApplyAddressCommand(object? obj);
        bool CanExecute_RetryNavigationCommand(object? obj);
        bool CanExecute_CurrentDocumentCommand(object? obj);
        bool CanExecute_OpenCommand(object? obj);
        bool CanExecute_OpenWithCommand(object? obj);
        bool CanExecute_RevealInExplorerCommand(object? obj);
        bool CanExecute_CopyPathCommand(object? obj);
        bool CanExecute_CutCommand(object? obj);
        bool CanExecute_CopyCommand(object? obj);
        bool CanExecute_PasteCommand(object? obj);
        bool CanExecute_DeleteCommand(object? obj);
        bool CanExecute_SortedCommand(object? obj);
        bool CanExecute_EncryptingKeyCommand(object? obj);
        bool CanExecute_EncryptCommand(object? obj);
        bool CanExecute_DecryptCommand(object? obj);
        bool CanExecute_CreateFileCommand(object? obj);
        bool CanExecute_CreateDirectoryCommand(object? obj);
        bool CanExecute_RenameClickCommand(object? obj);
        bool CanExecute_RenameCommand(object? obj);
        bool CanExecute_MoveCommand(object? obj);
        bool CanExecute_DropCommand(object? obj);
        bool CanExecure_RefreshCommand(object? obj);
        bool CanExecute_CancelRenameCommand(object? obj);
        bool CanExecute_TreeViewItemSelectedCommand (object? obj);
        bool CanExecute_ListViewItemDoubleClickCommand(object? obj);
        bool CanExecute_ListViewSelectionChangedCommand(object? obj);
        bool CanExecute_WindowSizeChanged(object? obj);


        void Execute_BackCommand(object? obj);
        void Execute_ForwardCommand(object? obj);
        void Execute_UpCommand(object? obj);
        void Execute_ApplyAddressCommand(object? obj);
        void Execute_CancelAddressCommand(object? obj);
        void Execute_RetryNavigationCommand(object? obj);
        void Execute_CurrentDocumentCommand(object? obj);
        void Execute_OpenCommand(object? obj);
        void Execute_OpenWithCommand(object? obj);
        void Execute_RevealInExplorerCommand(object? obj);
        void Execute_CopyPathCommand(object? obj);
        void Execute_CutCommand(object? obj);
        void Execute_CopyCommand(object? obj);
        void Execute_PasteCommand(object? obj);
        void Execute_DeleteCommand(object? obj);  
        void Execute_SortedCommand(object? obj);
        void Execute_EncryptingKeyCommand(object? obj);
        void Execute_EncryptCommand(object? obj);   
        void Execute_DecryptCommand(object? obj);
        void Execute_CreateFileCommand(object? obj);
        void Execute_CreateDirectoryCommand(object? obj);
        void Execute_RenameClickCommand(object? obj);
        void Execute_RenameCommand(object? obj);
        void Execute_MoveCommand(object? obj);
        void Execute_DropCommand(object? obj);
        void Execute_RefreshCommand(object? obj);
        void Execute_CancelRenameCommand(object? obj);
        void Execute_TreeViewItemSelectedCommand (object? obj);
        void Execute_ListViewItemDoubleClickCommand(object? obj);
        void Execute_ListViewSelectionChangedCommand(object? obj);
        void Execute_WindowSizeChanged(object? obj);
    }
}
