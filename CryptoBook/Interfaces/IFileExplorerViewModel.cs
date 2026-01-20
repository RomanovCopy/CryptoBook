using CryptoBook.DTO;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IFileExplorerViewModel:IViewModel,IWindowWithId
    {
        bool IsHiddenFilesVisible { get; set; }
        string CurrentPath { get; set; }
        object SelectedItem { get; set; }
        IDriveItem SelectedDrive{ get; set; }
        ReadOnlyObservableCollection<IFileSystemItem> GetFiles{ get; }
        ReadOnlyObservableCollection<IDriveItem> GetDrives{ get; }
        ReadOnlyObservableCollection<IDirectoryItem> GetDirectories { get; }

        ICommand CutCommand { get; }
        ICommand CopyCommand { get; }
        ICommand PasteCommand { get; }
        ICommand DeleteCommand { get; }
        ICommand CreateFileCommand { get; }
        ICommand CreateDirectoryCommand { get; }
        ICommand RenameCommand { get; }
        ICommand MoveCommand { get; }
        ICommand TreeViewItemSelectedCommand { get; }
    }
}
