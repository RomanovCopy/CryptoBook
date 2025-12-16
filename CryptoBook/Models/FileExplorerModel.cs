using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Models
{
    public class FileExplorerModel:ViewModelBase, IFileExplorerModel
    {

        private readonly IFileManagerService _fileManagerService;
        private readonly IFilePickerService _filePickerService;

        public Guid WindowId { get => _windowId; private set => SetProperty(ref _windowId, value); }
        private Guid _windowId;
        public bool IsHiddenFilesVisible { get => _isHiddenFilesVisible; set => SetProperty(ref _isHiddenFilesVisible, value); }
        private bool _isHiddenFilesVisible;
        public string CurrentPath { get => _currentPath; set => SetProperty(ref _currentPath, value); }
        private string _currentPath;
        public List<string> GetFiles{ get=>_files; private set=>SetProperty(ref _files, value); }
        private List<string> _files;
        public List<string> GetDirectories{ get=>_directories; private set=>SetProperty(ref _directories, value); }
        private List<string> _directories;


        public FileExplorerModel(IFileManagerService fileManagerService, IFilePickerService filePickerService)
        {
            _fileManagerService = fileManagerService;
            _filePickerService = filePickerService;
        }





        public bool CanExecute_CreateFile(object? obj)
        {
            throw new NotImplementedException();
        }

        public bool CanExecute_CreateDirectory(object? obj)
        {
            throw new NotImplementedException();
        }

        public bool CanExecute_DeleteFile(object? obj)
        {
            throw new NotImplementedException();
        }

        public bool CanExecute_DeleteDirectory(object? obj)
        {
            throw new NotImplementedException();
        }

        public bool CanExecute_MoveFile(object? obj)
        {
            throw new NotImplementedException();
        }

        public bool CanExecute_MoveDirectory(object? obj)
        {
            throw new NotImplementedException();
        }

        public void Execute_CreateFile(object? obj)
        {
            throw new NotImplementedException();
        }

        public void Execute_CreateDirectory(object? obj)
        {
            throw new NotImplementedException();
        }

        public void Execute_DeleteFile(object? obj)
        {
            throw new NotImplementedException();
        }

        public void Execute_DeleteDirectory(object? obj)
        {
            throw new NotImplementedException();
        }

        public void Execute_MoveFile(object? obj)
        {
            throw new NotImplementedException();
        }

        public void Execute_MoveDirectory(object? obj)
        {
            throw new NotImplementedException();
        }

        public bool CanExecute_Close(object? obj)
        {
            throw new NotImplementedException();
        }

        public void Execute_Loaded(object? obj)
        {
            throw new NotImplementedException();
        }

        public bool CanExecute_Loaded(object? obj)
        {
            throw new NotImplementedException();
        }

        public void Execute_Close(object? obj)
        {
            throw new NotImplementedException();
        }

        public bool CanExecute_Closing(object? obj)
        {
            throw new NotImplementedException();
        }

        public void Execute_Closing(object? obj)
        {
            throw new NotImplementedException();
        }

        public bool CanExecute_Closed(object? obj)
        {
            throw new NotImplementedException();
        }

        public void Execute_Closed(object? obj)
        {
            throw new NotImplementedException();
        }


    }
}
