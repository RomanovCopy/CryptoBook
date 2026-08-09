using CryptoBook.DTO;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IFileExplorerViewModel:
        IViewModel,
        IWindowOptions,
        IWindowWithId,
        ISortedCommand,
        IEncrypted,
        IDialogResult<string>,
        IConditionalDialogResult
    {
        double LeftColumnPercent { get; set; }
        double RightColumnPercent { get; set; }
        bool IsHiddenFilesVisible { get; set; }
        string CurrentPath { get; }
        string AddressText { get; set; }
        string FilterText { get; set; }
        ICollectionView ChildrenView { get; }
        bool IsCurrentDirectoryUnavailable { get; }
        FileExplorerNavigationErrorKind? LastNavigationError { get; }
        string NavigationErrorMessage { get; }
        string ExplorerTitle { get; }
        string PickerActionText { get; }
        string PickerSelectionPath { get; }
        bool IsPickerMode { get; }
        ISystemItem? SelectedItem { get; set; }
        ISystemItem? SelectedListItem { get; set; }
        IReadOnlyList<ISystemItem> SelectedItemsSnapshot { get; set; }
        ReadOnlyObservableCollection<IDriveItem> GetDrives { get; }
        IFavoriteDirectoriesViewModel Favorites { get; }
        IFilePreviewViewModel Preview { get; }

        ICommand BackCommand{ get; }
        ICommand ForwardCommand { get; }
        ICommand UpCommand { get; }
        ICommand ApplyAddressCommand { get; }
        ICommand CancelAddressCommand { get; }
        ICommand RetryNavigationCommand { get; }
        ICommand CurrentDocumentCommand { get; }
        ICommand OpenCommand { get; }
        ICommand OpenWithCommand { get; }
        ICommand RevealInExplorerCommand { get; }
        ICommand CopyPathCommand { get; }
        ICommand CutCommand { get; }
        ICommand CopyCommand { get; }
        ICommand PasteCommand { get; }
        ICommand DeleteCommand { get; }
        ICommand CreateFileCommand { get; }
        ICommand CreateDirectoryCommand { get; }
        ICommand RenameClickCommand {  get; }
        ICommand RenameCommand { get; }
        ICommand MoveCommand { get; }
        ICommand DropCommand { get; }
        ICommand RefreshCommand { get; }
        ICommand CancelRenameCommand { get; }
        ICommand TreeViewItemSelectedCommand { get; }
        ICommand ListViewItemDoubleClickCommand { get; }
        ICommand ListViewSelectionChangedCommand { get; }
        ICommand WindowSizeChangedCommand { get; }
        ICommand ConfirmSelectionCommand { get; }
        ICommand CancelSelectionCommand { get; }
    }
}
