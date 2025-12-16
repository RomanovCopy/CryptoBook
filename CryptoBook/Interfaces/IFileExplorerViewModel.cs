using System;
using System.Collections.Generic;
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
        List<string> GetFiles{ get; }
        List<string> GetDirectories{ get; }

        ICommand CreateFileCommand { get; }
        ICommand CreateDirectoryCommand { get; }
        ICommand DeleteFileCommand { get; }
        ICommand DeleteDirectoryCommand { get; }
        ICommand MoveFileCommand { get; }
        ICommand MoveDirectoryCommand { get; }

    }
}
