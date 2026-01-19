using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Views;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CryptoBook.Models
{
    public class FileExplorerModel: ViewModelBase, IFileExplorerModel
    {

        private readonly IFileManagerService _fileManagerService;
        private readonly IWindowManager _windowManager;
        private readonly IDriveManagerService _driveManagerService;

        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public Guid WindowId { get => _windowId; private set => SetProperty(ref _windowId, value); }
        private Guid _windowId;
        public bool IsHiddenFilesVisible { get => _isHiddenFilesVisible; set => SetProperty(ref _isHiddenFilesVisible, value); }
        private bool _isHiddenFilesVisible;
        public object? SelectedItem { get => _selectedItem; set => SetProperty(ref _selectedItem, value); }
        private object? _selectedItem;

        public DriveItem SelectedDrive { get => _selectedDrive; set => SetProperty(ref _selectedDrive, value); }
        private DriveItem _selectedDrive;
        public string CurrentPath { get => _currentPath; set => SetProperty(ref _currentPath, value); }
        private string _currentPath;
        public ReadOnlyObservableCollection<IFileSystemItem> GetFiles { get; private set; }
        private ObservableCollection<IFileSystemItem> _files;
        public ReadOnlyObservableCollection<DriveItem> GetDrives { get; private set; }
        private ObservableCollection<DriveItem> _drives;
        public ReadOnlyObservableCollection<IDirectoryItem> GetDirectories { get; private set; }
        private ObservableCollection<IDirectoryItem> _directories;



        public FileExplorerModel(IFileManagerService fileManagerService, IDriveManagerService driveManagerService, IWindowManager windowManager)
        {
            WindowId = Guid.NewGuid();
            _fileManagerService = fileManagerService;
            _driveManagerService = driveManagerService;
            _windowManager = windowManager;
            GetDrives = _driveManagerService.WritableDrives;
            _isHiddenFilesVisible = true;
            _directories = [];
            GetDirectories=new(_directories);
            _files = [];
            GetFiles=new(_files);
            CurrentPath = GetDrives.FirstOrDefault()?.RootDirectory ?? string.Empty;
        }

        private async void update(object o)
        {
            _files.Clear();
            var files = await _fileManagerService.BrowseAsync(path, _cancellationTokenSource.Token, isHidden);
            if(files == null) return;
            foreach(var file in files)
            {
                _files.Add(file);
            }
        }

        public bool CanExecute_CutCommand(object? obj)
        {
            throw new NotImplementedException();
        }

        public bool CanExecute_CopyCommand(object? obj)
        {
            throw new NotImplementedException();
        }

        public bool CanExecute_PasteCommand(object? obj)
        {
            throw new NotImplementedException();
        }

        public bool CanExecute_DeleteCommand(object? obj)
        {
            throw new NotImplementedException();
        }

        public bool CanExecute_CreateFileCommand(object? obj)
        {
            return true;
        }

        public bool CanExecute_CreateDirectoryCommand(object? obj)
        {
            return true;
        }
        public bool CanExecute_RenameCommand(object? obj)
        {
            throw new NotImplementedException();
        }

        public bool CanExecute_MoveCommand(object? obj)
        {
            throw new NotImplementedException();
        }


        public bool CanExecute_TreeViewItemSelectedCommand(object? obj)
        {
            return true;
        }




        public void Execute_CutCommand(object? obj)
        {
            throw new NotImplementedException();
        }

        public void Execute_CopyCommand(object? obj)
        {
            throw new NotImplementedException();
        }

        public void Execute_PasteCommand(object? obj)
        {
            throw new NotImplementedException();
        }

        public void Execute_DeleteCommand(object? obj)
        {
            throw new NotImplementedException();
        }

        public void Execute_CreateFileCommand(object? obj)
        {
            var id = _windowManager.CreateWindow<NewFileDialog>();
            _windowManager.ShowWindow(id);
        }

        public void Execute_CreateDirectoryCommand(object? obj)
        {
            throw new NotImplementedException();
        }
        public void Execute_RenameCommand(object? obj)
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

        public void Execute_MoveCommand(object? obj)
        {
            throw new NotImplementedException();
        }

        public void Execute_MoveDirectory(object? obj)
        {
            throw new NotImplementedException();
        }

        public async void Execute_TreeViewItemSelectedCommand(object? obj)
        {
            if((obj is IContainerFileSystemItem))
            {
                if(obj is IDirectoryItem directory)
                {

                } else if(obj is DriveItem drive)
                {
                }
            }
        }



        public bool CanExecute_Close(object? obj)
        {
            throw new NotImplementedException();
        }
        public void Execute_Close(object? obj)
        {
            throw new NotImplementedException();
        }

        public bool CanExecute_Loaded(object? obj)
        {
            throw new NotImplementedException();
        }
        public void Execute_Loaded(object? obj)
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
