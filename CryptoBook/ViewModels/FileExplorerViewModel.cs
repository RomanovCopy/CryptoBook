using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class FileExplorerViewModel: ViewModelBase, IFileExplorerViewModel
    {
        private readonly IFileExplorerModel _fileExplorerModel;

        public bool IsHiddenFilesVisible { get => _fileExplorerModel.IsHiddenFilesVisible; set => _fileExplorerModel.IsHiddenFilesVisible=value; }
        public List<string> GetFiles => _fileExplorerModel.GetFiles;
        public List<string> GetDirectories => _fileExplorerModel.GetDirectories;
        public string CurrentPath { get => _fileExplorerModel.CurrentPath; set => _fileExplorerModel.CurrentPath=value; }
        public Guid WindowId => _fileExplorerModel.WindowId;


        public FileExplorerViewModel(IFileExplorerModel fileExplorerModel)
        {
            _fileExplorerModel = fileExplorerModel ?? throw new ArgumentNullException(nameof(fileExplorerModel));
        }



        public ICommand CreateFileCommand => throw new NotImplementedException();

        public ICommand CreateDirectoryCommand => throw new NotImplementedException();

        public ICommand DeleteFileCommand => throw new NotImplementedException();

        public ICommand DeleteDirectoryCommand => throw new NotImplementedException();

        public ICommand MoveFileCommand => throw new NotImplementedException();

        public ICommand MoveDirectoryCommand => throw new NotImplementedException();

        public ICommand Loaded => throw new NotImplementedException();

        public ICommand Close => throw new NotImplementedException();

        public ICommand Closing => throw new NotImplementedException();

        public ICommand Closed => throw new NotImplementedException();

    }
}
