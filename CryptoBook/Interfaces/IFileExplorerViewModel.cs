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
    public interface IFileExplorerViewModel:IViewModel,IWindowOptions,IWindowWithId
    {
        double LeftCololumnPercent { get; set; }
        double RightColumnPercent{  get; set; }
        bool IsHiddenFilesVisible { get; set; }
        ISystemItem? SelectedItem { get; set; }
        ReadOnlyObservableCollection<IDriveItem> GetDrives{ get; }

        ICommand CutCommand { get; }
        ICommand CopyCommand { get; }
        ICommand PasteCommand { get; }
        ICommand DeleteCommand { get; }
        ICommand CreateFileCommand { get; }
        ICommand CreateDirectoryCommand { get; }
        ICommand RenameCommand { get; }
        ICommand MoveCommand { get; }
        ICommand RefreshCommand { get; }
        ICommand TreeViewItemSelectedCommand { get; }
    }
}
