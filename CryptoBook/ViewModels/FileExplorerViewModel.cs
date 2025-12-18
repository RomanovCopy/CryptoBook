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


        public FileExplorerViewModel( IFileExplorerModel fileExplorerModel)
        {
            _fileExplorerModel = fileExplorerModel ?? throw new ArgumentNullException(nameof(fileExplorerModel));
        }



        public ICommand CreateFileCommand => _createFileCommand ??= new RelayCommand(_fileExplorerModel.Execute_CreateFile, _fileExplorerModel.CanExecute_CreateFile);
        RelayCommand _createFileCommand;

        public ICommand CreateDirectoryCommand => _createDirectoryCommand ??= new RelayCommand(_fileExplorerModel.Execute_CreateDirectory, _fileExplorerModel.CanExecute_CreateDirectory);
        RelayCommand _createDirectoryCommand;

        public ICommand RenameFileCommand => _renameFileCommand ??= new RelayCommand(_fileExplorerModel.Execute_RenameFile, _fileExplorerModel.CanExecute_RenameFile);
        RelayCommand _renameFileCommand;

        public ICommand DeleteFileCommand => _deleteFileCommand ??= new RelayCommand(_fileExplorerModel.Execute_DeleteFile, _fileExplorerModel.CanExecute_DeleteFile);
        RelayCommand _deleteFileCommand;

        public ICommand DeleteDirectoryCommand => _deleteDirectoryCommand ??= new RelayCommand(_fileExplorerModel.Execute_DeleteDirectory, _fileExplorerModel.CanExecute_DeleteDirectory);
        RelayCommand _deleteDirectoryCommand;

        public ICommand MoveFileCommand => _moveFileCommand ??= new RelayCommand(_fileExplorerModel.Execute_MoveFile, _fileExplorerModel.CanExecute_MoveFile);
        RelayCommand _moveFileCommand;

        public ICommand MoveDirectoryCommand => _moveDirectoryCommand ??= new RelayCommand(_fileExplorerModel.Execute_MoveDirectory, _fileExplorerModel.CanExecute_MoveDirectory);
        RelayCommand _moveDirectoryCommand;

        public ICommand Loaded => _loadedCommand ??= new RelayCommand(_fileExplorerModel.Execute_Loaded, _fileExplorerModel.CanExecute_Loaded);
        RelayCommand _loadedCommand;

        public ICommand Close => _closeCommand ??= new RelayCommand(_fileExplorerModel.Execute_Close, _fileExplorerModel.CanExecute_Close);
        RelayCommand _closeCommand;

        public ICommand Closing => _closingCommand ??= new RelayCommand(_fileExplorerModel.Execute_Closing, _fileExplorerModel.CanExecute_Closing);
        RelayCommand _closingCommand;

        public ICommand Closed => _closedCommand ??= new RelayCommand(_fileExplorerModel.Execute_Closed, _fileExplorerModel.CanExecute_Closed);
        RelayCommand _closedCommand;

    }
}
